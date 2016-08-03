using System;
using Windows.UI.Xaml.Media.Imaging;

namespace GIFEditor
{
    public sealed class ImageProcessor
    {
        private WriteableBitmap[] originalArray;
        private WriteableBitmap[] currentArray;

        public ImageProcessor(WriteableBitmap[] frameArray)
        {
            if (frameArray == null || frameArray.Length == 0) throw new NullReferenceException();
            currentArray = frameArray;
            originalArray = frameArray;
        }//constructor

        public WriteableBitmap[] GetChangedArray(int brightness, int contrast, bool changeOriginalArray)
        {
            if (changeOriginalArray) currentArray = originalArray;
            //if changeOriginalArray == true, we work with original array (in case we choose a filter)
            //if changeOriginalArray == false, we work with current array (in case we modify filter chosen before)

            WriteableBitmap[] result = new WriteableBitmap[currentArray.Length];
            byte[] rawData;

            for (int i = 0; i < currentArray.Length; i++)
            {
                rawData = currentArray[i].ToByteArray(); //work with bytes of every image
                double contrastLevel = Math.Pow(((100.0 + contrast) / 100.0), 2); //a piece of our algorythm

                for (int k = 0; k < rawData.Length; k++)
                {
                    if (k % 4 != 3) // if k % 4 == 3 -- we`re on transparency byte -- we don`t need to modify it
                    rawData[k] = CheckPixelValue(((((rawData[k] / 255.0 - 0.5) * contrastLevel) + 0.5) * 255.0) + brightness);
                }//some algorythm for every byte of current image

                result[i] = new WriteableBitmap(currentArray[i].PixelWidth, currentArray[i].PixelHeight);
                result[i].FromByteArray(rawData, 0, rawData.Length);
                //write raw data to new image
            }

            if (changeOriginalArray) currentArray = result;
            //if changeOriginalArray == true, we work with chosing a filter, assign our array as actual to 
            //modify it later

            return result;
        }

        public WriteableBitmap[] GetGreyAndSet()
        {
            currentArray = originalArray;
            WriteableBitmap[] result = new WriteableBitmap[currentArray.Length];
            for (int i = 0; i < currentArray.Length; i++)
                result[i] = currentArray[i].Gray();
            currentArray = result;
            return result;
        }//set current array to grey filter

        public WriteableBitmap[] GetInvertedAndSet()
        {
            currentArray = originalArray;
            WriteableBitmap[] result = new WriteableBitmap[currentArray.Length];
            byte[] rawData;
            for (int i = 0; i < currentArray.Length; i++)
            {
                rawData = currentArray[i].ToByteArray();
                for (int k = 0; k < rawData.Length; k++)
                {
                    if (k % 4 != 3) 
                    rawData[k] = CheckPixelValue(255 - rawData[k]);
                }
                result[i] = new WriteableBitmap(currentArray[i].PixelWidth, currentArray[i].PixelHeight);
                result[i].FromByteArray(rawData, 0, rawData.Length);
            }
            currentArray = result;
            return result;
        }//set current array to inverted filter

        public WriteableBitmap[] OriginalArray
        {
            get { currentArray = originalArray; return currentArray; }
        }//refrest current array and return it

        private byte CheckPixelValue(double pixel)
        {
            if (pixel > 255) return 255;
            if (pixel < 0) return 0;
            return (byte)pixel;
        }

    }
}
