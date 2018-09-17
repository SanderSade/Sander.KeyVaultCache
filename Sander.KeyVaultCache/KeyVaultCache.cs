using System;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.Azure.KeyVault.Models;
using Microsoft.Azure.KeyVault.WebKey;

namespace Sander.KeyVaultCache
{
	/// <summary>
	/// Key vault cache. Use this class as singleton/static per KeyVault instance
	/// </summary>
	public sealed class KeyVaultCache
	{
		private readonly KeyFetcher _keyFetcher;


		/// <summary>
		/// Create a new instance of KeyVaultCache, defining expiration duration for caching
		/// <para>Use TimeSpan.Zero for infinite duration (no expiration)</para>
		/// </summary>
		/// <param name="keyVaultClient">Existing KeyVault client instance</param>
		/// <param name="cachingDuration">Caching duration. Use TimeSpan.Zero for infinite caching (no expiration)</param>
		public KeyVaultCache(IKeyVaultClient keyVaultClient, TimeSpan cachingDuration)
		{
			if (keyVaultClient == null)
				throw new NullReferenceException($"{nameof(keyVaultClient)} is null!");

			_keyFetcher = new KeyFetcher(keyVaultClient, cachingDuration);
		}


		/// <summary>
		/// Create a new instance of KeyVaultCache, defining expiration duration for caching in seconds)
		/// <para>Using expiration of 0 seconds results infinite duration (no expiration)</para>
		/// </summary>
		/// <param name="keyVaultClient">Existing KeyVault client instance</param>
		/// <param name="cachingDurationSeconds">Cching duration in seconds. Use 0 for infinite caching (no expiration)</param>
		public KeyVaultCache(IKeyVaultClient keyVaultClient, uint cachingDurationSeconds) : this(keyVaultClient,
			TimeSpan.FromSeconds(cachingDurationSeconds))
		{
		}



		/// <summary>
		/// Create a new instance of KeyVaultCache without defining expiration (cached values do not expire)
		/// </summary>
		/// <param name="keyVaultClient">Existing KeyVault client instance</param>
		public KeyVaultCache(IKeyVaultClient keyVaultClient) : this(keyVaultClient, TimeSpan.Zero)
		{
		}

		/// <summary>
		/// Remove specific item from cache. Does not get error when the item does not exist in cache
		/// </summary>
		public void Remove(string name)
		{
			_keyFetcher.Remove(name);
		}

		/// <summary>
		/// Remove all entries from cache.
		/// </summary>
		public void Clear()
		{
			_keyFetcher.Clear();
		}

		/// <summary>
		/// Get secret bundle (secret value and its metadata), optionally refetching the value
		/// </summary>
		/// <param name="name">Full URL to the entry in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<SecretBundle> GetSecretBundle(string name, bool forceRefetch = false)
		{
			return await _keyFetcher.GetBundle<SecretBundle>(name, forceRefetch).ConfigureAwait(false);
		}


		/// <summary>
		/// Get certificate bundle (certificate and its metadata), optionally refetching the value
		/// </summary>
		/// <param name="name">Full URL to the entry in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<CertificateBundle> GetCertificateBundle(string name, bool forceRefetch = false)
		{
			return await _keyFetcher.GetBundle<CertificateBundle>(name, forceRefetch).ConfigureAwait(false);
		}


		/// <summary>
		/// Get key bundle (key and its metadata), optionally refetching the value
		/// </summary>
		/// <param name="name">Full URL to the entry in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<KeyBundle> GetKeyBundle(string name, bool forceRefetch = false)
		{
			return await _keyFetcher.GetBundle<KeyBundle>(name, forceRefetch).ConfigureAwait(false);
		}


		/// <summary>
		/// Get secret value, optionally forcing the refetch from Azure
		/// </summary>
		/// <param name="name">Full URL to the entry in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<string> GetSecret(string name, bool forceRefetch = false)
		{
			var bundle = await GetSecretBundle(name, forceRefetch);
			return bundle?.Value;
		}


		/// <summary>
		/// Get certificate value as a byte array, optionally forcing the refetch from Azure
		/// </summary>
		/// <param name="name">Full URL to the entry in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<byte[]> GetCertificate(string name, bool forceRefetch = false)
		{
			var bundle = await GetCertificateBundle(name, forceRefetch);
			return bundle?.Cer;
		}


		/// <summary>
		/// Get key/JsonWebKey, optionally forcing the refetch from Azure
		/// </summary>
		/// <param name="name">Full URL to the entry in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<JsonWebKey> GetKey(string name, bool forceRefetch = false)
		{
			var bundle = await GetKeyBundle(name, forceRefetch);
			return bundle?.Key;
		}
	}
}
