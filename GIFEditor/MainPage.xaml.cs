using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.ApplicationModel.Activation;
using Windows.ApplicationModel.Resources;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;

namespace GIFEditor
{
    public sealed partial class MainPage : Page
    {
        //fields
        bool fileKindIsPhoto;
        WriteableBitmap[] frameArray;


        public MainPage()
        {
            this.InitializeComponent();
            this.NavigationCacheMode = NavigationCacheMode.Required;
        }//constructor


        private string Localize(string str)
        {
            return ResourceLoader.GetForCurrentView().GetString(str);
        }//english and russian language support

        private void DisposeArray()
        {
            frameArray = new WriteableBitmap[0];
            GC.Collect();
        }//delete chosen photos

        private void ShowProgressBar()
        {
            statusTextBlock.Text = String.Format("{0}... .", Localize("rendering"));
            imagesSourceButton.Visibility = Visibility.Collapsed;
            gifSourceButton.Visibility = Visibility.Collapsed;
            cameraSourceButton.Visibility = Visibility.Collapsed;
            infoButton.Visibility = Visibility.Collapsed;
            LoadingBar.IsEnabled = true;
            LoadingBar.Visibility = Visibility.Visible;
        }//show progressBar and hide all page elements

        private void HideProgressBar()
        {
            statusTextBlock.Text = String.Format("{0} .", Localize("create"));
            imagesSourceButton.Visibility = Visibility.Visible;
            gifSourceButton.Visibility = Visibility.Visible;
            cameraSourceButton.Visibility = Visibility.Visible;
            infoButton.Visibility = Visibility.Visible;
            LoadingBar.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Collapsed;
        }//hide progressBar and show all page elements

        private async Task<WriteableBitmap[]> GetFramesFromImagesAsync(IReadOnlyList<StorageFile> argsFiles)
        {
            //fill imagesArray with empty data
            var imagesArray = new WriteableBitmap[argsFiles.Count];

            //flags-on
            bool onlysquare = true;
            bool onlyhorizontal = true;
            bool onlyvertical = true;

            for (int i = 0; i < argsFiles.Count; i++)
            {
                //create frame from file (some black magic in these lines of code LOL -- not to use meta-data)
                WriteableBitmap bufferFrame = new WriteableBitmap(1, 1); 
                bufferFrame.SetSource(await argsFiles[i].OpenReadAsync() as IRandomAccessStream);
                bufferFrame = new WriteableBitmap(bufferFrame.PixelWidth, bufferFrame.PixelHeight);
                bufferFrame.SetSource(await argsFiles[i].OpenReadAsync() as IRandomAccessStream);
                imagesArray[i] = bufferFrame;

                //manipulation with flags
                if (!((int)bufferFrame.PixelWidth == (int)bufferFrame.PixelHeight)) onlysquare = false;
                if (!((int)bufferFrame.PixelWidth > (int)bufferFrame.PixelHeight)) onlyhorizontal = false;
                if (!((int)bufferFrame.PixelWidth < (int)bufferFrame.PixelHeight)) onlyvertical = false;

                //dispose bufferFrame
                bufferFrame = null;
            }

            //crop images smartly
            for (int i = 0; i < argsFiles.Count; i++)
            {
                bool squareForm = ((int)imagesArray[i].PixelWidth == (int)imagesArray[i].PixelHeight);
                bool horizontalForm = ((int)imagesArray[i].PixelWidth > (int)imagesArray[i].PixelHeight);

                //if all images are typical, just resize them, else resize and make them square
                if (onlysquare || onlyhorizontal || onlyvertical)
                {
                    if ((int)imagesArray[i].PixelWidth > 360 || (int)imagesArray[i].PixelHeight > 360)
                    {
                        if (onlysquare)
                            imagesArray[i] = imagesArray[i].Resize(360, 360, WriteableBitmapExtensions.Interpolation.Bilinear);
                        else if (onlyvertical)
                            imagesArray[i] = imagesArray[i].Resize(360, (int)((double)imagesArray[i].PixelHeight / (double)imagesArray[i].PixelWidth * 360), WriteableBitmapExtensions.Interpolation.Bilinear);
                        else imagesArray[i] = imagesArray[i].Resize((int)((double)imagesArray[i].PixelWidth / (double)imagesArray[i].PixelHeight * 360), 360, WriteableBitmapExtensions.Interpolation.Bilinear);
                    }
                }
                else
                {
                    if (squareForm)
                    {
                        if ((int)imagesArray[i].PixelWidth > 360)
                            imagesArray[i] = imagesArray[i].Resize(360, 360, WriteableBitmapExtensions.Interpolation.Bilinear);
                    }
                    else
                    {
                        if ((int)imagesArray[i].PixelWidth > 360 || (int)imagesArray[i].PixelHeight > 360)
                            imagesArray[i] = imagesArray[i].Resize(((horizontalForm) ? 640 : 360),
                                ((horizontalForm) ? 360 : 640),
                                WriteableBitmapExtensions.Interpolation.Bilinear);
                        imagesArray[i] = imagesArray[i].Crop((horizontalForm) ? 140 : 0, (horizontalForm) ? 0 : 140, 360, 360);
                    }
                }
            }
            return imagesArray;
        }//get animation frames from images chosen from image library

        private async Task<WriteableBitmap[]> GetFramesFromGifAsync(StorageFile storageFile)
        {
            //create empty array
            WriteableBitmap[] imageArray;

            //open chosen file and use NOKIA`s algorythm
            using (var fileStream = await storageFile.OpenAsync(FileAccessMode.Read))
            {
                var bitmapDecoder = await BitmapDecoder.CreateAsync(BitmapDecoder.GifDecoderId, fileStream);
                byte[] firstFrame = null;
                imageArray = new WriteableBitmap[bitmapDecoder.FrameCount];
                if (bitmapDecoder.FrameCount > 30) throw new Exception("GIF containts too much frames.");
                if (bitmapDecoder.FrameCount == 0) throw new Exception("GIF does not contain frames");
                for (uint frameIndex = 0; frameIndex < bitmapDecoder.FrameCount; frameIndex++)
                {
                    var frame = await bitmapDecoder.GetFrameAsync(frameIndex);
                    var frameBitmapTarget = new WriteableBitmap((int)frame.OrientedPixelWidth, (int)frame.OrientedPixelHeight);
                    var data = await frame.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Premultiplied,
                        new BitmapTransform(),
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.DoNotColorManage);
                    var frameBytes = data.DetachPixelData();
                    if (firstFrame == null)
                    {
                        firstFrame = frameBytes;
                    }
                    else
                    {
                        for (uint i = 0; i < frameBytes.Count(); i += 4)
                        {
                            int alpha = frameBytes[i + 3];
                            if (alpha == 0 && firstFrame != null)
                            {
                                Array.Copy(firstFrame, (int)i, frameBytes, (int)i, 4);
                            }
                        }
                    }
                    using (var stream = frameBitmapTarget.PixelBuffer.AsStream())
                    {
                        stream.Write(frameBytes, 0, frameBytes.Length);
                    }
                    frameBitmapTarget.Invalidate();
                    imageArray[frameIndex] = frameBitmapTarget;
                    frameBitmapTarget = null;
                }
            }
            return imageArray;
        }//get animation frames from GIF-file chosen from image library

        public async void ContinueFileOpenPicker(FileOpenPickerContinuationEventArgs args)
        {
            try
            {
                ShowProgressBar();

                //if something bad didn`t happen
                if (args.Files.Count > 0)
                {
                    //if we opened photoes
                    if (fileKindIsPhoto)
                    {
                        // [1..30] images
                        if (args.Files.Count < 1 || args.Files.Count > 30) throw new NotSupportedException(Localize("imagesError"));

                        //get frames from chosen photos and continue
                        frameArray = await GetFramesFromImagesAsync(args.Files);
                        Frame.Navigate(typeof(Edit), frameArray);
                    }
                    else
                    {
                        // [1] GIF
                        if (args.Files.Count != 1) throw new NotSupportedException(Localize("gifError"));

                        //get frames from chosen GIF and continue
                        frameArray = await GetFramesFromGifAsync(args.Files[0]);
                        Frame.Navigate(typeof(Edit), frameArray);
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                frameArray = new WriteableBitmap[0];
                GC.Collect();
                Frame.Navigate(typeof(MainPage));
            }
            catch (NotSupportedException ex)
            {
                var dlg = new MessageDialog(ex.Message);
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception ex)
            {
                var dlg = new MessageDialog(ex.Message);
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            finally
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200));
                HideProgressBar();
            }
        }//continue to edit after taking frames from chosen files



        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            DisposeArray();
        }//dispose photos from previous sesion

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            DisposeArray();
            base.OnNavigatedFrom(e);
        }//dispose chosen photos



        private void cameraSourceButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Capture));
        }//capture frames with camera

        private void imagesSourceButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker imagesPicker = new FileOpenPicker();
            imagesPicker.ViewMode = PickerViewMode.Thumbnail;
            imagesPicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
            imagesPicker.FileTypeFilter.Add(".bmp");
            imagesPicker.FileTypeFilter.Add(".jpg");
            imagesPicker.FileTypeFilter.Add(".png");
            fileKindIsPhoto = true;
            imagesPicker.PickMultipleFilesAndContinue();
        }//select images

        private void gifSourceButton_Click(object sender, RoutedEventArgs e)
        {
            FileOpenPicker gifPicker = new FileOpenPicker();
            gifPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            gifPicker.ViewMode = PickerViewMode.Thumbnail;
            gifPicker.FileTypeFilter.Add(".gif");
            fileKindIsPhoto = false;
            gifPicker.PickSingleFileAndContinue();
        }//select GIF-file

        private void infoButton_Click(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(Info));
        }//see info

    }
}
