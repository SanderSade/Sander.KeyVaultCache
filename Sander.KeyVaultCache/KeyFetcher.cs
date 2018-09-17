﻿using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Caching;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;

[assembly: InternalsVisibleTo("Sander.KeyVaultCache.Tests")]

namespace Sander.KeyVaultCache
{
	internal sealed class KeyFetcher
	{
		private readonly TimeSpan _cachingDuration;
		private readonly IKeyVaultClient _keyVaultClient;
		private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks;
		private readonly MemoryCache _valueCache;
		private readonly TimeSpan _kvWaitTime = TimeSpan.FromSeconds(16);


		/// <inheritdoc />
		internal KeyFetcher(IKeyVaultClient keyVaultClient, TimeSpan cachingDuration)
		{
			_keyVaultClient = keyVaultClient;
			_cachingDuration = cachingDuration;
			_valueCache = new MemoryCache(nameof(Sander.KeyVaultCache));
			_locks = new ConcurrentDictionary<string, SemaphoreSlim>();
		}


		internal void Remove(string name)
		{
			var semaphore = _locks.GetOrAdd(string.Intern(name), new SemaphoreSlim(1, 1));

			semaphore.Wait(_kvWaitTime);
			Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Removing {name} from cache");

			_valueCache.Remove(name);

			if (semaphore.CurrentCount == 0)
				semaphore.Release();
		}


		internal async Task<T> GetBundle<T>(string name, bool forceRefetch) where T : class
		{
			Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Requesting {typeof(T).Name} from {name}, force refetch: {forceRefetch}");

			if (forceRefetch || !_valueCache.Contains(name))
			{
				//The idea is to allow only one fetch per key, and should two threads ask for the same key, one thread waits and returns the value cached by the other key
				//Cannot use lock() {} here, as await is not allowed inside lock block.
				var semaphore = _locks.GetOrAdd(string.Intern(name), new SemaphoreSlim(1, 1));

				await semaphore.WaitAsync(_kvWaitTime)
							   .ConfigureAwait(false); //if there is no response by this time, the previous request has gone bad

				try
				{
					if (forceRefetch || !_valueCache.Contains(name))
					{
						_valueCache.Remove(name);

						var value = await FetchValue<T>(name);

						if (value == null)
							throw new NullReferenceException($"Key Vault request {typeof(T).Name} for \"{name}\" returned null!");

						var cachePolicy = new CacheItemPolicy();

						if (_cachingDuration != TimeSpan.Zero)
							cachePolicy.AbsoluteExpiration = DateTimeOffset.UtcNow.AddMilliseconds(_cachingDuration.TotalMilliseconds);

						_valueCache.Add(name, value, cachePolicy);

						Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Added to cache: {name}");
						return value;
					}
				}
				finally
				{
					if (semaphore.CurrentCount == 0)
						semaphore.Release();
				}
			}

			if (!(_valueCache.Get(name) is T cachedValue))
				throw new InvalidCastException($"Returned value from KeyVault cache is null or not castable to {typeof(T).Name}?!");

			Debug.WriteLine($"[{DateTimeOffset.UtcNow:O}] Fetched from cache: {name}");
			return cachedValue;
		}


		private async Task<T> FetchValue<T>(string name) where T : class
		{
			var type = typeof(T);
			switch (true)
			{
				case bool _ when type == typeof(SecretBundle):
					return await _keyVaultClient.GetSecretAsync(name).ConfigureAwait(false) as T;
				case bool _ when type == typeof(CertificateBundle):
					return await _keyVaultClient.GetCertificateAsync(name).ConfigureAwait(false) as T;
				case bool _ when type == typeof(KeyBundle):
					return await _keyVaultClient.GetKeyAsync(name).ConfigureAwait(false) as T;
				default:
					throw new NotImplementedException($"Oops?! Wrong type in {nameof(KeyFetcher)}.{nameof(GetBundle)}");
			}
		}


		internal void Clear()
		{
			foreach (var pair in _valueCache)
			{
				Remove(pair.Key);
			}
			//_valueCache?.Dispose();
			//_valueCache = new MemoryCache(nameof(Sander.KeyVaultCache));
			//_locks = new ConcurrentDictionary<string, SemaphoreSlim>();
		}
	}
}
