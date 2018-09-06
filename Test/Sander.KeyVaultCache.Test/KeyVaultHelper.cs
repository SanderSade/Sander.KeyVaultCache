using System;
using System.Configuration;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Sander.KeyVaultCache.Test
{
	/// <summary>
	/// Helper class to access KeyVault
	/// </summary>
	internal sealed class KeyVaultHelper : KeyVaultClient
	{
		private static string _applicationId;
		private static string _certificateThumbPrint;


		/// <inheritdoc />
		/// <summary>
		/// This should not be used for anything besides testing
		/// </summary>
		internal KeyVaultHelper(string applicationId, string certificateThumbPrint) : base(AuthenticationToken)
		{
			_applicationId = applicationId;

			//we've had weird invisible character issues with people pasting cert values from Windows cert info
			var cleanedString = certificateThumbPrint.Where(c => char.IsDigit(c) ||
																 c >= 'a' && c <= 'f' ||
																 c >= 'A' && c <= 'F');

			_certificateThumbPrint = new string(cleanedString.ToArray());
		}


		/// <summary>
		/// Authentication method for KeyVaultClient callback
		/// </summary>
		/// <param name="authContext"></param>
		/// <param name="resource"></param>
		/// <param name="scope"></param>
		/// <returns></returns>
		private static async Task<string> AuthenticationToken(string authContext, string resource, string scope)
		{
			using (var certificate = FindCertByThumbPrint(StoreName.My, StoreLocation.LocalMachine, _certificateThumbPrint))
			{
				if (certificate == null)
					throw new ConfigurationErrorsException(FormattableString.Invariant($"Certificate not found for thumbprint '{_certificateThumbPrint}'"));

				var clientAssertionCertificate = new ClientAssertionCertificate(_applicationId, certificate);

				// setup context, authenticate
				var authenticationContext = new AuthenticationContext(authContext);
				var authenticationResult = await authenticationContext.AcquireTokenAsync(resource, clientAssertionCertificate).ConfigureAwait(false);

				if (authenticationResult == null)
				{
					throw new InvalidOperationException(
						FormattableString.Invariant($"Failed to authenticate to context '{authContext}', resource '{resource}', scope: '{scope}'"));
				}

				return authenticationResult.AccessToken;
			}
		}


		/// <summary>
		/// Certificate search helper function
		/// </summary>
		/// <param name="storeName"></param>
		/// <param name="storeLoc"></param>
		/// <param name="certThumbPrint"></param>
		/// <returns></returns>
		private static X509Certificate2 FindCertByThumbPrint(StoreName storeName, StoreLocation storeLoc, string certThumbPrint)
		{
			using (var store = new X509Store(storeName, storeLoc))
			{
				store.Open(OpenFlags.ReadOnly);
				var certificateCollection = store.Certificates.Find(X509FindType.FindByThumbprint, certThumbPrint, false);

				if (certificateCollection.Count > 0)
					return certificateCollection[0];

				throw new ApplicationException(FormattableString.Invariant(
					$"Certificate not found for thumbprint '{certThumbPrint}' in StoreLocation '{storeLoc}', StoreName '{storeName}'."));
			}
		}
	}
}
