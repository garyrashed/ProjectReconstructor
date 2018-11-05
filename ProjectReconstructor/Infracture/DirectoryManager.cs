using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using log4net;

namespace ProjectReconstructor.Infracture
{

    public static class DirectoryManager
    {
        public static void Copy(string sourceDirectory, string targetDirectory, ILog logger = null)
        {
            DirectoryInfo diSource = new DirectoryInfo(sourceDirectory);
            DirectoryInfo diTarget = new DirectoryInfo(targetDirectory);

            CopyAll(diSource, diTarget);
        }

        public static bool CopyAll(DirectoryInfo source, DirectoryInfo target, ILog logger = null)
        {
            var success = true;

            try
            {
                Directory.CreateDirectory(target.FullName);
            }
            catch (Exception e)
            {
                success = false;
                logger?.Error($"Error creating folder. {target.FullName}");
            }
            
            
            // Copy each file into the new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                logger?.Info($"Copying {target.FullName} to {fi.Name}");
                try
                {
                    fi.CopyTo(Path.Combine(target.FullName, fi.Name), true);
                }
                catch (Exception e)
                {
                    success = false;
                    logger?.Error($"Error copying file {target.FullName} to {fi.Name}.");
                    logger?.Error(e);
                }
                
            }

            var subFoldersWereSuccessfull = true;
            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                var wasSuccesful = CopyAll(diSourceSubDir, nextTargetSubDir);
                if (wasSuccesful == false)
                    subFoldersWereSuccessfull = false;
            }

            return success && subFoldersWereSuccessfull;
        }


    }

    // Output will vary based on the contents of the source directory.
    
}
