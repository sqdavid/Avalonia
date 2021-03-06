// Copyright (c) The Avalonia Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Avalonia.Platform;

namespace Avalonia.Shared.PlatformSupport
{
    /// <summary>
    /// Loads assets compiled into the application binary.
    /// </summary>
    public class AssetLoader : IAssetLoader
    {
        private static readonly Dictionary<string, AssemblyDescriptor> AssemblyNameCache
            = new Dictionary<string, AssemblyDescriptor>();

        private AssemblyDescriptor _defaultAssembly;

        public AssetLoader(Assembly assembly = null)
        {
            if (assembly == null)
                assembly = Assembly.GetEntryAssembly();
            if (assembly != null)
                _defaultAssembly = new AssemblyDescriptor(assembly);
        }

        public void SetDefaultAssembly(Assembly assembly)
        {
            _defaultAssembly = new AssemblyDescriptor(assembly);
        }

        /// <summary>
        /// Checks if an asset with the specified URI exists.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="baseUri">
        /// A base URI to use if <paramref name="uri"/> is relative.
        /// </param>
        /// <returns>True if the asset could be found; otherwise false.</returns>
        public bool Exists(Uri uri, Uri baseUri = null)
        {
            return GetAsset(uri, baseUri) != null;
        }

        /// <summary>
        /// Opens the resource with the requested URI.
        /// </summary>
        /// <param name="uri">The URI.</param>
        /// <param name="baseUri">
        /// A base URI to use if <paramref name="uri"/> is relative.
        /// </param>
        /// <returns>A stream containing the resource contents.</returns>
        /// <exception cref="FileNotFoundException">
        /// The resource was not found.
        /// </exception>
        public Stream Open(Uri uri, Uri baseUri = null)
        {
            var asset = GetAsset(uri, baseUri);

            if (asset == null)
            {
                throw new FileNotFoundException($"The resource {uri} could not be found.");
            }

            return asset.GetStream();
        }

        private IAssetDescriptor GetAsset(Uri uri, Uri baseUri)
        {
            if (!uri.IsAbsoluteUri || uri.Scheme == "resm")
            {
                var uriQueryParams = ParseQueryString(uri);
                var baseUriQueryParams = uri != null ? ParseQueryString(uri) : null;
                var asm = GetAssembly(uri) ?? GetAssembly(baseUri) ?? _defaultAssembly;

                if (asm == null && _defaultAssembly == null)
                {
                    throw new ArgumentException(
                        "No default assembly, entry assembly or explicit assembly specified; " +
                        "don't know where to look up for the resource, try specifiyng assembly explicitly.");
                }

                IAssetDescriptor rv;

                var resourceKey = uri.AbsolutePath;

#if __IOS__
                // TODO: HACK: to get iOS up and running. Using Shared projects for resources
                // is flawed as this alters the reource key locations across platforms
                // I think we need to use Portable libraries from now on to avoid that.
                if(asm.Name.Contains("iOS"))
                {
                    resourceKey = resourceKey.Replace("TestApplication", "Avalonia.iOSTestApplication");
                }
#endif

                asm.Resources.TryGetValue(resourceKey, out rv);
                return rv;
            }
            throw new ArgumentException($"Invalid uri, see https://github.com/AvaloniaUI/Avalonia/issues/282#issuecomment-166982104", nameof(uri));
        }

        private AssemblyDescriptor GetAssembly(Uri uri)
        {
            if (uri != null)
            {
                var qs = ParseQueryString(uri);
                string assemblyName;

                if (qs.TryGetValue("assembly", out assemblyName))
                {
                    return GetAssembly(assemblyName);
                }
            }

            return null;
        }

        private AssemblyDescriptor GetAssembly(string name)
        {
            if (name == null)
            {
                return _defaultAssembly;
            }

            AssemblyDescriptor rv;
            if (!AssemblyNameCache.TryGetValue(name, out rv))
            {
                var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
                var match = loadedAssemblies.FirstOrDefault(a => a.GetName().Name == name);
                if (match != null)
                {
                    AssemblyNameCache[name] = rv = new AssemblyDescriptor(match);
                }
                else
                {
                    // iOS does not support loading assemblies dynamically!
                    //
#if !__IOS__
                    AssemblyNameCache[name] = rv = new AssemblyDescriptor(Assembly.Load(name));
#endif
                }
            }

            return rv;
        }

        private Dictionary<string, string> ParseQueryString(Uri uri)
        {
            return uri.Query.TrimStart('?')
                .Split(new[] { '&' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Split('='))
                .ToDictionary(p => p[0], p => p[1]);
        }

        private interface IAssetDescriptor
        {
            Stream GetStream();
        }

        private class AssemblyResourceDescriptor : IAssetDescriptor
        {
            private readonly Assembly _asm;
            private readonly string _name;

            public AssemblyResourceDescriptor(Assembly asm, string name)
            {
                _asm = asm;
                _name = name;
            }

            public Stream GetStream()
            {
                return _asm.GetManifestResourceStream(_name);
            }
        }

        private class AssemblyDescriptor
        {
            public AssemblyDescriptor(Assembly assembly)
            {
                Assembly = assembly;

                if (assembly != null)
                {
                    Resources = assembly.GetManifestResourceNames()
                        .ToDictionary(n => n, n => (IAssetDescriptor)new AssemblyResourceDescriptor(assembly, n));
                    Name = assembly.GetName().Name;
                }
            }

            public Assembly Assembly { get; }
            public Dictionary<string, IAssetDescriptor> Resources { get; }
            public string Name { get; }
        }
    }
}
