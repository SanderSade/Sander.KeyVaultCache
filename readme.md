[![GitHub license](https://img.shields.io/badge/licence-MPL%202.0-brightgreen.svg)](https://github.com/SanderSade/UrlShortener/blob/master/LICENSE)
[![NetStandard 2.0](https://img.shields.io/badge/-.NET%20Standard%202.0-green.svg)](https://github.com/dotnet/standard/blob/master/docs/versions/netstandard2.0.md)


## Introduction
KeyVaultCache is read-only cache as system-of-record pattern implementation for [Azure Key Vault](https://azure.microsoft.com/en-us/services/key-vault/).

What this means in humanese is that KeyVaultCache takes care of fetching and caching of the values from the Azure Key Vault.

As secrets and certificates change very rarely, it makes sense to cache them, as fetching is a relatively slow operation - if the Key Vault is in the same data center, it takes 100...300ms, but fetching value from geographically distant Azure data center can take a second or even more.

KeyVaultCache simplifies the fetching and caching of the values from Key Vault. It is fully thread-safe, has on-demand re-fetching and supports cache expiration (entries expire after specified time).


## Features
* Easy to use
* On-demand re-fetching of the values from Key Vault (for example, if accessing blob storage fails, first step would be to re-fetch the secret and try again)
* Supports Key Vault secrets, certificates and keys
* Methods to return either bundle (e.g. SecretBundle, CertificateBundle, KeyBundle) or values directly (string for secret, byte array for certificate, [JsonWebKey (JWK)](https://tools.ietf.org/html/rfc7517) for key).
* Optional cache expiration (absolute time - entries expire after specified time and will be re-fetched when requested).
* Fully thread-safe (one thread can request re-fetch of the secret, other threads will wait until fetching completes and use the new value).
* .NET Standard 2.0, meaning KeyVaultCache can be used with .NET Framework 4.6.1+, .NET Core 2.0 and more - see [here](https://github.com/dotnet/standard/blob/master/docs/versions.md) for detailed information.

#### Dependencies
* [Microsoft.Azure.KeyVault](https://www.nuget.org/packages/Microsoft.Azure.KeyVault/) 3+ recommended, 2.0.6 or newer required
* [System.Runtime.Caching](https://www.nuget.org/packages/System.Runtime.Caching/) 4.5.0+

## Use & examples
KeyVaultCache is meant to be used as a single instance per Key Vault. You can store the KeyVaultCache instance in a static variable or configure your IoC/DI framework accordingly.

**Simple use**
```
Shared.KeyVaultCache = new KeyVaultCache(new KeyVaultClient("your-authentication-token"));
...
...
var storageAccountConnectionString =
	$"DefaultEndpointsProtocol=https;AccountName={BlobStorageName};AccountKey={await Shared.KeyVaultCache.GetSecret("https://your-key-vault.vault.azure.net/secrets/storageaccesskey")};EndpointSuffix=core.windows.net"; 
```

First line will create the KeyVaultCache instance where values never expire.   
After that we'll use that instance to fetch the Azure Storage access key to be used in a connection string.


**Example with expiration and retry**
```
Shared.KeyVaultCache = new KeyVaultCache(new KeyVaultClient("your-authentication-token"),  TimeSpan.FromMinutes(15));
...
...
public async Task<string> GetUsername(long userId, bool isRetry = false)
{
	try
	{
		var certificateBytes = Shared.KeyVaultCache.GetCertificate("https://your-key-vault.vault.azure.net/certificates/yourcertificate", isRetry);
		//this can write certificate to Windows temp folder, so don't do it!
		var accessCertificate = new X509Certificate2(certificateBytes);
		//fetch username
...
		return username;

	}
	catch(Exception ex)
	{
		Trace.Writeline(ex);
		if (isRetry)
			throw;

		return await GetUsername(userId, true);
	}
}
```
In this example we create an instance of the KeyVaultCache where all cached values will expire in 15 minutes after initial fetching, so next request for cached value will re-fetch the value from Azure and cache it again for 15 minutes.

The method GetUsername() has parameter isRetry with a default value of false. This allows us to implement a very simple retry mechanism - if the initial (isRetry == false) username request fails with exception, we'll try once more. And the second attempt with isRetry == true also re-requests the certificate from the Azure Key Vault - maybe a new certificate was added to the data store and Key Vault.

This allows very simple key rotation without any downtime for the service, as is often required by various govermental and other entities.

For example, if our resource is Azure Storage account, we have primary accesskey in the Key Vault which has been fetched and cached by our application. We'll regenerate the _secondary_ access key and store it to KeyVault instead of the primary key - and regenerate the primary key, invalidating our cached value. This means that our next attempt to access storage account fails, but we will go and fetch the new valid value from the Key Vault - and can re-attempt the operation without any downtime.



## FAQ
* How to use KeyVaultCache methods from non-async method?  
`var secret = Shared.KeyVaultCache.GetSecret("url-here").GetAwaiter().GetResult();`
* How to manually remove value from cache?  
`keyVaultCache.Remove("url-here");` will remove just the specified value.  
`keyVaultCache.Clear();` removes all cached entries.
Both operations are thread-safe.
  




## Changelog
* 1.0.0 Initial release
