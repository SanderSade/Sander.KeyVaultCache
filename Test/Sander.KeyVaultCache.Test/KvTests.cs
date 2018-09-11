using System;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Sander.KeyVaultCache.Test
{
	[TestClass]
	public class KvTests
	{
		private readonly KeyVaultCache _kv;
		private readonly string _secret1;


		/// <inheritdoc />
		public KvTests()
		{
			var keyVaultHelper = new KeyVaultHelper(ConfigurationManager.AppSettings["ApplicationId"],
				ConfigurationManager.AppSettings["ApplicationCertificate"]);
			_kv = new KeyVaultCache(keyVaultHelper);
			_secret1 = ConfigurationManager.AppSettings["Secret1"];
		}


		[TestMethod]
		public void EnsureCachingWorks()
		{
			var sw1 = Stopwatch.StartNew();
			var uncached = _kv.GetSecret(_secret1, true).GetAwaiter().GetResult();
			sw1.Stop();
			var sw2 = Stopwatch.StartNew();
			var cached = _kv.GetSecret(_secret1).GetAwaiter().GetResult();
			sw2.Stop();

			Assert.AreEqual(uncached, cached);
			Assert.IsTrue(sw1.ElapsedTicks > sw2.ElapsedTicks);
			Trace.WriteLine($"Uncached: {sw1.ElapsedMilliseconds}, cached {sw2.ElapsedMilliseconds} ms");
		}


		[TestMethod]
		public void EnsureCacheLifetime()
		{
			var keyVaultHelper = new KeyVaultHelper(ConfigurationManager.AppSettings["ApplicationId"],
				ConfigurationManager.AppSettings["ApplicationCertificate"]);
			var kv = new KeyVaultCache(keyVaultHelper, 4);

			var sw1 = Stopwatch.StartNew();
			var uncached = kv.GetSecret(_secret1, true).GetAwaiter().GetResult();
			sw1.Stop();

			var sw2 = Stopwatch.StartNew();
			var cached = kv.GetSecret(_secret1).GetAwaiter().GetResult();
			sw2.Stop();

			Thread.Sleep(5000);

			var sw3 = Stopwatch.StartNew();
			var refetch = kv.GetSecret(_secret1).GetAwaiter().GetResult();
			sw3.Stop();

			Trace.WriteLine($"Uncached: {sw1.ElapsedMilliseconds}, cached: {sw2.ElapsedMilliseconds}, refetch: {sw3.ElapsedMilliseconds} ms");

			Assert.AreEqual(uncached, cached);
			Assert.AreEqual(refetch, cached);
			Assert.IsTrue(sw1.ElapsedMilliseconds > 100);
			Assert.IsTrue(sw2.ElapsedMilliseconds < 10);
			Assert.IsTrue(sw3.ElapsedMilliseconds > 100);
		}


		[TestMethod]
		public void EnsureRemovalWorks()
		{
			EnsureCachingWorks();
			var sw1 = Stopwatch.StartNew();
			_kv.Remove(_secret1);
			var uncached = _kv.GetSecret(_secret1).GetAwaiter().GetResult();
			sw1.Stop();
			Trace.WriteLine($"Uncached: {sw1.ElapsedMilliseconds} ms");
			Assert.IsTrue(sw1.ElapsedMilliseconds > 100);
		}


		[TestMethod]
		public void EnsureClearWorks()
		{
			EnsureCachingWorks();
			var sw1 = Stopwatch.StartNew();
			_kv.Clear();
			var uncached = _kv.GetSecret(_secret1).GetAwaiter().GetResult();
			sw1.Stop();
			Trace.WriteLine($"Uncached: {sw1.ElapsedMilliseconds} ms");
			Assert.IsTrue(sw1.ElapsedMilliseconds > 100);
		}


		[ExpectedException(typeof(KeyVaultErrorException))]
		[TestMethod]
		public void NotFound()
		{
			_kv.GetSecret(_secret1 + ".abc").GetAwaiter().GetResult();
		}


		[ExpectedException(typeof(ArgumentException))]
		[TestMethod]
		public void WrongKvType()
		{
			_kv.GetCertificate(_secret1).GetAwaiter().GetResult();
		}


		[ExpectedException(typeof(InvalidCastException))]
		[TestMethod]
		public void WrongCacheType()
		{
			_kv.GetSecret(_secret1).GetAwaiter().GetResult();
			_kv.GetCertificateBundle(_secret1).GetAwaiter().GetResult();
		}


		[TestMethod]
		public void BundleHandling()
		{
			var bundle = _kv.GetSecretBundle(_secret1).GetAwaiter().GetResult();
			Assert.IsTrue(_secret1.EndsWith(bundle.SecretIdentifier.Name));
		}



		/// <summary>
		/// Just eyeballing the results here
		/// </summary>
		[TestMethod]
		public void MultiThreaded()
		{
			Enumerable.Range(0, 16)
			          .AsParallel()
			          .WithDegreeOfParallelism(16)
			          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
			          .ForAll(_ => { _kv.GetSecret(_secret1).GetAwaiter().GetResult(); });
		}


		/// <summary>
		/// Just eyeballing the results here
		/// </summary>
		[TestMethod]
		public void MultiThreadedWithRefetch()
		{
			Enumerable.Range(0, 16)
			          .AsParallel()
			          .WithDegreeOfParallelism(16)
			          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
			          .ForAll(i => { _kv.GetSecret(_secret1, i % 3 == 0).GetAwaiter().GetResult(); });
		}



		/// <summary>
		/// Just eyeballing the results here
		/// </summary>
		[TestMethod]
		public void MultiThreadedWithRemove()
		{
			Enumerable.Range(0, 16)
			          .AsParallel()
			          .WithDegreeOfParallelism(16)
			          .WithExecutionMode(ParallelExecutionMode.ForceParallelism)
			          .ForAll(i =>
			                  {
								  if (i % 5 == 0)
									  _kv.Remove(_secret1);

				                  _kv.GetSecret(_secret1, false).GetAwaiter().GetResult();
			                  });
		}
	}
}
