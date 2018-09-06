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
		/// Create a new instance of KeyVaultCache, optionally defining expiration duration for caching
		/// <para>Omitting the duration or using in the default TimeSpan instance marks infinite duration (no expiration)</para>
		/// </summary>
		/// <param name="keyVaultClient">Exsisting KeyVault client instance</param>
		/// <param name="cachingDuration">Optional caching duration. Omit the parameter for infinite caching (no expiration)</param>
		public KeyVaultCache(KeyVaultClient keyVaultClient, TimeSpan cachingDuration = default(TimeSpan))
		{
			if (keyVaultClient == null)
				throw new NullReferenceException($"{nameof(keyVaultClient)} is null!");

			_keyFetcher = new KeyFetcher(keyVaultClient, cachingDuration);
		}


		/// <summary>
		/// Create a new instance of KeyVaultCache, optionally defining expiration duration for caching
		/// <para>Omitting the cachingDurationSeconds or using in 0 results infinite duration (no expiration)</para>
		/// </summary>
		/// <param name="keyVaultClient">Exsisting KeyVault client instance</param>
		/// <param name="cachingDurationSeconds">Optional caching duration in seconds. Omit the parameter or use 0 for infinite caching (no expiration)</param>
		public KeyVaultCache(KeyVaultClient keyVaultClient, uint cachingDurationSeconds = 0) : this(keyVaultClient,
			TimeSpan.FromSeconds(cachingDurationSeconds))
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
		/// Get secret bundle (secret value and its metadata), optionally refetching the value
		/// </summary>
		/// <param name="name">Name in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<SecretBundle> GetSecretBundle(string name, bool forceRefetch = false)
		{
			return await _keyFetcher.GetBundle<SecretBundle>(name, forceRefetch);
		}


		/// <summary>
		/// Get certificate bundle (certificate and its metadata), optionally refetching the value
		/// </summary>
		/// <param name="name">Name in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<CertificateBundle> GetCertificateBundle(string name, bool forceRefetch = false)
		{
			return await _keyFetcher.GetBundle<CertificateBundle>(name, forceRefetch);
		}


		/// <summary>
		/// Get key bundle (key and its metadata), optionally refetching the value
		/// </summary>
		/// <param name="name">Name in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<KeyBundle> GetKeyBundle(string name, bool forceRefetch = false)
		{
			return await _keyFetcher.GetBundle<KeyBundle>(name, forceRefetch);
		}


		/// <summary>
		/// Get secret value, optionally forcing the refetch from Azure
		/// </summary>
		/// <param name="name">Name in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<string> GetSecret(string name, bool forceRefetch = false)
		{
			var bundle = await GetSecretBundle(name, forceRefetch);
			return bundle?.Value;
		}


		/// <summary>
		/// Get certificate value as a byte array, optionally forcing the refetch from Azure
		/// </summary>
		/// <param name="name">Name in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<byte[]> GetCertificate(string name, bool forceRefetch = false)
		{
			var bundle = await GetCertificateBundle(name, forceRefetch);
			return bundle?.Cer;
		}


		/// <summary>
		/// Get key/JsonWebKey, optionally forcing the refetch from Azure
		/// </summary>
		/// <param name="name">Name in Azure KeyVault</param>
		/// <param name="forceRefetch">Set to true to force refetch from Azure</param>
		public async Task<JsonWebKey> GetKey(string name, bool forceRefetch = false)
		{
			var bundle = await GetKeyBundle(name, forceRefetch);
			return bundle?.Key;
		}
	}
}
