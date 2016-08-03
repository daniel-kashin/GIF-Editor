using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Devices.Enumeration;
using Windows.ApplicationModel.Resources;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using Lumia.Imaging;
using Lumia.Imaging.Transforms;

namespace GIFEditor
{

    public sealed partial class Capture : Page
    {
        //fields
        DeviceInformation backCamera;
        DeviceInformation frontCamera;
        DeviceInformation currentCamera;
        CameraPreviewImageSource cameraSource;
        bool isPreviewing;
        bool isRendering;
        bool isCapturing;
        bool isPreviEwenabled;
        bool capturingmodeIsPhoto;
        WriteableBitmapRenderer bitmapRenderer;
        WriteableBitmap currentWriteableBitmap;
        WriteableBitmap[] frameArray;
        int frameIndex;
        int maxFrameIndex;


        public Capture()
        {
            InitializeComponent();
        }//constructor


        private string Localize(string str)
        {
            return ResourceLoader.GetForCurrentView().GetString(str);
        }//english and russian language support

        private void changeMaxFrameIndexBarLabelRerfesh()
        {
            changeMaxFrameIndexBar.Label = Localize("frames") + maxFrameIndex;
        }//refresh changeMaxFrameIndexBarLabel

        private void capturingModeBarLabelRefresh()
        {
            capturingModeBar.Label = (capturingmodeIsPhoto) ? Localize("photo")
                : Localize("animation");
        }//refresh capturingModeBarLabel

        private void frameIndexBoxRefresh()
        {
            statusTextBlock.Text = string.Format("{0}: {1}/{2} .", Localize("capturing"), frameIndex, maxFrameIndex);
        }//refresh frameIndexBox

        public void StopPreviewRenderAndCapture()
        {
            isPreviewing = false; isRendering = false;
            isCapturing = false;
        }//change flags

        private void DisposeArray()
        {
            previewImage.Source = null;
            isCapturing = false;
            isCapturing = false;
            frameArray = new WriteableBitmap[0];
            frameIndex = 0;
            if (isPreviewing)
                frameIndexBoxRefresh();
            GC.Collect();
        }//delete captured frames and stop capturing

        private void HideCommandBar()
        {
            changeCameraButton.Visibility = Visibility.Collapsed;
            commandBar.Visibility = Visibility.Collapsed;
        }//hide commandBar

        private void ShowCommandBar()
        {
            commandBar.Visibility = Visibility.Visible;
            changeCameraButton.Visibility = Visibility.Visible;
        }//show commandBar

        private void ShowProgressBar()
        {
            statusTextBlock.Text = Localize("initialization");
            LoadingBar.IsEnabled = true;
            LoadingBar.Visibility = Visibility.Visible;
        }//show progressBar while initializing camera

        private void HideProgressBar()
        {
            applicationName.Visibility = Visibility.Visible;
            if (isPreviewing)
                frameIndexBoxRefresh();
            LoadingBar.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Collapsed;
        }//hide progressBar

        private async Task DisposeResourcesAsync()
        {
            HideCommandBar();
            DisposeArray();
            if (bitmapRenderer != null) bitmapRenderer.Dispose();
            bitmapRenderer = null;

            if (isPreviewing && (cameraSource != null))
            {
                await cameraSource.StopPreviewAsync();
                cameraSource.Dispose();
            }
            cameraSource = null;
            GC.Collect();
        }//dispose page resources

        private async Task FindBackAndFrontCameraAsync()
        {
            //assign back and front camera
            backCamera = (await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture))
                   .FirstOrDefault(x => x.EnclosureLocation != null &&
                   x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Back);
            frontCamera = (await DeviceInformation.FindAllAsync(DeviceClass.VideoCapture))
                   .FirstOrDefault(x => x.EnclosureLocation != null &&
                   x.EnclosureLocation.Panel == Windows.Devices.Enumeration.Panel.Front);

            //if device has backCamera, it`s active, else if device has frontCamera, it`s active, else throw exception
            if (backCamera != null) currentCamera = backCamera;
            else currentCamera = frontCamera;
            if (currentCamera == null) throw new Exception(Localize("camsError"));

            //manipulation with indexes and capturing mode
            maxFrameIndex = 30;
            capturingModeBarLabelRefresh();
            changeMaxFrameIndexBarLabelRerfesh();
            if (isPreviewing)
                frameIndexBoxRefresh();
        }//find backCamera and frontCamera



        private async Task InitializePreviewAsync()
        {
            //initialize cameraSource
            cameraSource = new CameraPreviewImageSource();
            await cameraSource.InitializeAsync(currentCamera.Id);

            //supply rendering cameraSource`s frames
            var properties = await cameraSource.StartPreviewAsync();
            cameraSource.PreviewFrameAvailable += OnPreviewFrameAvailable;
            isPreviewing = true; isPreviEwenabled = false;
            HideProgressBar();

            //crop frames smartly
            int width; int height;
            if (properties.Width < Width || properties.Height < Height)
            {
                width = (int)properties.Width; height = (int)properties.Height;
            }
            else
            {
                width = 360; height = 640;
            }

            //connect rendered bitmap with image
            currentWriteableBitmap = new WriteableBitmap(width, height);
            image.Source = currentWriteableBitmap;

            //create rotation effecr
            int rotationIndex = (currentCamera == backCamera) ? 90 : 270;
            RotationFilter _rotationFilter = new RotationFilter(rotationIndex);
            var _filters = new List<IFilter>();
            _filters.Add(_rotationFilter);
            var _effect = new FilterEffect(cameraSource);
            _effect.Filters = _filters;

            //finally create renderer based on effect and bitmap we are going to render
            bitmapRenderer = new WriteableBitmapRenderer(_effect, currentWriteableBitmap);

        }//initialize preview resources

        private async void OnPreviewFrameAvailable(IImageSize args)
        {
            try
            {
                //render bitmap
                var renderTask = RenderAsync();
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception ex)
            {
                if (ex.HResult != -2005270523)
                {
                    Frame.Navigate(typeof(MainPage));
                }
            }
        }//render available bitmap

        private async Task RenderAsync()
        {
            //if preview is on and rendering of the previous frame finished
            if (!isRendering && isPreviewing)
            {
                //flag-on
                isRendering = true;

                //render frame
                await bitmapRenderer.RenderAsync();

                //clone frame to array
                await Windows.ApplicationModel.Core.CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(
                    CoreDispatcherPriority.High, (DispatchedHandler)(() =>
                    {
                        //if capture is on and array is not filled
                        if (isCapturing && frameIndex < maxFrameIndex)
                        {
                            //clone rendered frame
                            var newimage = currentWriteableBitmap.Clone();

                            //add cloned frame
                            Array.Resize(ref frameArray, frameArray.Length + 1);
                            frameArray[frameIndex] = newimage;

                            //go to editing if array is filled
                            if (frameIndex + 1 == maxFrameIndex)
                                Frame.Navigate(typeof(Edit), frameArray);

                            //copy frame to previewImage if such option is enabled
                            if (isPreviEwenabled)
                                previewImage.Source = newimage;

                            //change frameIndex
                            frameIndex++;
                            frameIndexBoxRefresh();

                            //stop capturing if capturingmodeIsPhoto
                            isCapturing = !capturingmodeIsPhoto;

                            //dispose cloned image
                            newimage = null;
                        }

                        //invalidate currentWriteableBitmap
                        currentWriteableBitmap.Invalidate();
                        GC.Collect();
                    }));

                //flag-off
                isRendering = false;

            }
        }//render available frame



        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                base.OnNavigatedTo(e);
                ShowProgressBar();
                await DisposeResourcesAsync();
                await FindBackAndFrontCameraAsync();
                StopPreviewRenderAndCapture();
                await InitializePreviewAsync();
                ShowCommandBar();
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception ex)
            {
                if (ex.HResult == -402587628) //microsoft error, doesn`t matter
                {
                    var dlg = new MessageDialog(Localize("somethingwent"));
                    await dlg.ShowAsync();
                }
                Frame.Navigate(typeof(MainPage));
            }
        }//upload page resources

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            try
            {
                await DisposeResourcesAsync();
                base.OnNavigatedFrom(e);
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception ex)
            {
                if (ex.HResult != -2005270523)
                {
                    Frame.Navigate(typeof(MainPage));
                }
            }
        }//dispose page resources



        private void againBar_Click(object sender, RoutedEventArgs e)
        {
            DisposeArray();
        }//dispose captured array

        private void cameraBar_Click(object sender, RoutedEventArgs e)
        {
            if (frameIndex == maxFrameIndex && !capturingmodeIsPhoto)
                DisposeArray();
            isCapturing = !isCapturing;
        }//spart/pause capturing

        private async void acceptBar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (frameArray == null || frameArray.Length < 1) throw new Exception();
                Frame.Navigate(typeof(Edit), frameArray);
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception)
            {
                statusTextBlock.Text = Localize("nothingtosave");
                await Task.Delay(TimeSpan.FromSeconds(1));
                frameIndexBoxRefresh();
            }

        }//go to editing

        private void capturingModeBar_Click(object sender, RoutedEventArgs e)
        {
            DisposeArray();
            capturingmodeIsPhoto = (capturingmodeIsPhoto) ? false : true;
            capturingModeBarLabelRefresh();
        }//dispose captured array and change capturing mode

        private void changeMaxFrameImdexBar_Click(object sender, RoutedEventArgs e)
        {
            DisposeArray();
            maxFrameIndex = (maxFrameIndex == 15) ? 30 : 15;
            changeMaxFrameIndexBarLabelRerfesh();
            frameIndexBoxRefresh();
        }//dispose captured array and change maxFrameIndex

        private void enablePreviewImageBar_Click(object sender, RoutedEventArgs e)
        {
            isPreviEwenabled = !isPreviEwenabled;
            if (!isPreviEwenabled) previewImage.Source = null;
            enablePreviewImageBar.Label = (isPreviEwenabled) ? Localize("previewon") : Localize("previewoff");
        }//enable/disable preview

        private async void changeCameraButton_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            try
            {
                string errorText = "";
                if (currentCamera == backCamera)
                {
                    if (frontCamera != null)
                        currentCamera = frontCamera;
                    else errorText = Localize("errorFront");
                }
                else
                if (currentCamera == frontCamera)
                {
                    if (backCamera != null)
                        currentCamera = backCamera;
                    else errorText = Localize("errorBack");
                }

                //if camera change cannot be done, show error message, else change currentCamera
                if (errorText != "")
                {
                    var dlg = new MessageDialog(errorText);
                    await dlg.ShowAsync();
                }
                else
                {
                    ShowProgressBar();
                    await DisposeResourcesAsync();
                    StopPreviewRenderAndCapture();
                    await InitializePreviewAsync();
                    ShowCommandBar();
                }
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception ex)
            {
                if (ex.HResult != -2005270523)
                {
                    Frame.Navigate(typeof(MainPage));
                }
            }
        }
        //switch current camera if it`s possible
    }
}
