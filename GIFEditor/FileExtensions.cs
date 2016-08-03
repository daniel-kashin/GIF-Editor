using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace GIFEditor
{
    public static class FileExtensions
    {
        public static async Task<bool> FileExists(this StorageFolder folder, string fileName)
        {
            try { StorageFile file = await folder.GetFileAsync(fileName); }
            catch
            {
                return false; //file does not exist 
            }
            return true;//file exists

        }//extension method to check if file exists in chosen folder or not

    }
}
