using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;

namespace Talisman
{

    // --------------------------------------------------------------------------
    /// <summary>
    /// Helper methods for working with assemblies
    /// </summary>
    // --------------------------------------------------------------------------
    public class AssemblyHelper 
    {
        // --------------------------------------------------------------------------
        /// <summary>
        /// Get resource text using a loose naming scheme
        /// </summary>
        // --------------------------------------------------------------------------
        public static string GetResourceText(string name)
        {
            var thisAssembly = Assembly.GetExecutingAssembly();
            string foundName = null;
            foreach (var resourceName in thisAssembly.GetManifestResourceNames())
            {
                if (name == resourceName) foundName = resourceName;
                if (foundName == null && resourceName.ToLower().EndsWith(name.ToLower()))
                {
                    foundName = resourceName;
                }
            }

            if (foundName == null)
            {
                throw new ApplicationException("Could not find resource named: " + name);
            }

            using (var reader = new StreamReader(thisAssembly.GetManifestResourceStream(foundName)))
            {
                return reader.ReadToEnd();
            }
        }
    }
}
