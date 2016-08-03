using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Windows.Storage;
using Windows.ApplicationModel.Resources;
using Windows.UI;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Lumia.Imaging;
using Lumia.InteropServices.WindowsRuntime;

namespace GIFEditor
{

    public sealed partial class Edit : Page
    {
        //fields
        SymbolIcon playIcon = new SymbolIcon(Symbol.Play);
        SymbolIcon pauseIcon = new SymbolIcon(Symbol.Pause);
        WriteableBitmap[] frameArray;
        DispatcherTimer animationTimer;
        int frameIndex;
        int rate;
        string filename;
        string lastButtonName;
        bool isprocessing;
        ImageProcessor imageProcessor;


        public Edit()
        {
            this.InitializeComponent();

            SetButtonColor(filter1);

            doubleAnimation.Duration = TimeSpan.FromMilliseconds(80);

            speedSlider.ValueChanged += SpeedSlider_ValueChanged;
        }//constructor


        private string Localize(string str)
        {
            return ResourceLoader.GetForCurrentView().GetString(str);
        }//english and russian language support

        private void SetButtonColor(Button button)
        {
            Button[] buttons = { filter1, filter2, filter3, filter4, filter5, filter6, filter7, filter8, filter9 };
            for (int i=0; i<buttons.Length;i++)
            {
                buttons[i].BorderBrush = buttons[i].Foreground = new SolidColorBrush(Colors.White);
            }

            button.BorderBrush = button.Foreground = new SolidColorBrush(Colors.Crimson);
        }//set choosen filter`s button color to crimson

        private void ShowProgressBar(string text)
        {
            isprocessing = true;
            defaultButton.IsEnabled = settingsButton.IsEnabled = playButton.IsEnabled = saveButton.IsEnabled = false;
            statusTextBlock.Text = text + "... .";
            LoadingBar.IsEnabled = true;
            LoadingBar.Visibility = Visibility.Visible;
        }//show progress bar while doing something
            
        private void HideProgressBar(string text)
        {
            isprocessing = false;
            defaultButton.IsEnabled = settingsButton.IsEnabled = playButton.IsEnabled = true;
            if (settingsGrid.Opacity != 1.0)
                 saveButton.IsEnabled = true;
            statusTextBlock.Text = text;
            LoadingBar.IsEnabled = false;
            LoadingBar.Visibility = Visibility.Collapsed;
        }//hide progress bar

        private async Task SaveImagesAsGifFileAsync(WriteableBitmap[] imageArray)
        {
            try
            {
                //convert images from imageArray to IBuffer (to save it as GIF-file then)
                GC.Collect();
                List<IImageProvider> imageProviders = new List<IImageProvider>();
                for (int i = 0; i < imageArray.Length; i++)
                {
                    var buffFrame = imageArray[i];
                    var buffBitmap = buffFrame.AsBitmap();
                    var bufferSource = new BitmapImageSource(buffBitmap);
                    imageProviders.Add(bufferSource);
                }
                GC.Collect();
                GifRenderer gifRenderer = new GifRenderer();
                gifRenderer.Duration = rate;
                gifRenderer.NumberOfAnimationLoops = 10000;
                gifRenderer.UseGlobalPalette = false;
                gifRenderer.Sources = imageProviders;
                await Task.Delay(TimeSpan.FromSeconds(1));
                Windows.Storage.Streams.IBuffer buff = await gifRenderer.RenderAsync();
                
                //crete/open folder
                StorageFolder folder;
                try { folder = await KnownFolders.PicturesLibrary.GetFolderAsync("GIF Editor"); }
                catch (Exception)
                {
                    folder = await KnownFolders.PicturesLibrary.CreateFolderAsync("GIF Editor");
                }

                //generate next unique name
                ulong num = 0;
                do
                {
                    num++;
                    filename = num + ".gif";
                } while (await folder.FileExists(filename));

                //save file
                var storageFile = await folder.CreateFileAsync(filename, CreationCollisionOption.GenerateUniqueName);
                using (var memoryStream = await storageFile.OpenAsync(FileAccessMode.ReadWrite))
                {
                    await Task.Delay(TimeSpan.FromSeconds(1));
                    await memoryStream.WriteAsync(buff);
                }
                gifRenderer.Dispose();
                imageProviders = null;
                GC.Collect();
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
            }
            catch (Exception)
            {
                Frame.Navigate(typeof(MainPage));
            }

        }//convert images to GIF-file and save it

        private async Task RefreshAnimationAsync(bool isSecondaryProcess)
        {
            //if isprocessing == true, we shouldn`t handle user requests 
            //isSecondaryProcess == true -- force to run the process
            if (!isprocessing || isSecondaryProcess)
            {

                //isSecondaryProcess == true -- code does not affect on ProgressBar
                if (!isSecondaryProcess)
                {
                    ShowProgressBar(Localize("processing"));
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                }

                try
                {
                    //change animation
                    frameArray = imageProcessor.GetChangedArray((int)brightSlider.Value, (int)contrastSlider.Value, false);
                }
                catch (OutOfMemoryException)
                {
                    var dlg = new MessageDialog(Localize("memoryError"));
                    await dlg.ShowAsync();
                }
                catch (Exception)
                {
                    Frame.Navigate(typeof(MainPage));
                }
                finally
                {
                    //isSecondaryProcess == true -- code does not affect on ProgressBar
                    if (!isSecondaryProcess)
                    {
                        HideProgressBar(Localize("editing"));
                        Play();
                    }
                }
            }
        }//change frameArray to match sliders` current values

        private void DefaultSliderValues()
        {
            brightSlider.Value = 0;
            contrastSlider.Value = 0;
            speedSlider.Value = 100;
        }//change sliders` values to default values



        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            GC.Collect();
            try
            {
                //show command bar
                commandBar.Visibility = Visibility.Visible;
                commandBar.IsEnabled = true;

                //upload frameArray
                frameArray = e.Parameter as WriteableBitmap[];
                if (frameArray == null) throw new Exception();

                //fit frameArray to the screen
                if (frameArray[0].PixelWidth >= frameArray[0].PixelHeight)
                    image.Stretch = Windows.UI.Xaml.Media.Stretch.Uniform;
                else image.Stretch = Windows.UI.Xaml.Media.Stretch.UniformToFill;

                //upload imageProcessor
                imageProcessor = new ImageProcessor(frameArray);

                InitializeTimer();
                DefaultSliderValues();
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
                Frame.Navigate(typeof(MainPage));
            }
            catch (Exception)
            {
                Frame.Navigate(typeof(MainPage));
            }
        }//upload page resources

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            try
            {
                DisposeTimer();
                GC.Collect();
                base.OnNavigatedFrom(e);
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
            }
            catch
            {
                Frame.Navigate(typeof(MainPage));
            }
        }//dispose the page

        private async void pointerExited(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            //false means that RefreshAnimationAsync is the main process
            await RefreshAnimationAsync(false);
        }//refresh currentArray

        private void SpeedSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            rate = (int)speedSlider.Value;
            animationTimer.Interval = new TimeSpan(0, 0, 0, 0, rate);
        }//change rate of animation

        private void doubleAnimation_Completed(object sender, object e)
        {
            if (doubleAnimation.From == 0.0)
            {
                doubleAnimation.From = 1.0;
                doubleAnimation.To = 0.0;
            }
            else
            {
                settingsGrid.Visibility = Visibility.Collapsed;
                doubleAnimation.From = 0.0;
                doubleAnimation.To = 1.0;
            }
        }//manipulate settings bar`s animation



        private void Play()
        {
            if (animationTimer != null && !animationTimer.IsEnabled)
                animationTimer.Start();
            playButton.Icon = pauseIcon;
        }//start playing animation

        private void Stop()
        {
            if (animationTimer != null && animationTimer.IsEnabled)
                animationTimer.Stop();
            playButton.Icon = playIcon;
        }//stop playing animation

        private async void AnimationTimer_Tick(object sender, object e)
        {
            try
            {
                if (frameArray != null && frameArray.Length > 0)
                {
                    //loop animation and show frames one-by-one
                    frameIndex = (frameIndex < frameArray.Length - 1) ? frameIndex + 1 : 0;
                    image.Source = frameArray[frameIndex];
                }
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
            }
            catch (Exception)
            {
                Frame.Navigate(typeof(MainPage));
            }
        }//show one frame

        private async void InitializeTimer()
        {
            try
            {
                animationTimer = new DispatcherTimer();
                animationTimer.Tick += AnimationTimer_Tick;
                rate = 100;
                animationTimer.Interval = new TimeSpan(0, 0, 0, 0, rate);
                frameIndex = 0;
                if (!animationTimer.IsEnabled) Play();
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
            }
            catch (Exception)
            {
                Frame.Navigate(typeof(MainPage));
            }
        }//initialize timer

        private void StopTimer()
        {
            try
            {
                if (animationTimer != null && animationTimer.IsEnabled)
                {
                    Stop();
                }
                image.Source = null;
                frameIndex = 0;
            }
            catch (Exception)
            {
                Frame.Navigate(typeof(MainPage));
            }
        }//stop timer

        private void DisposeTimer()
        {
            StopTimer();
            animationTimer = null;
        }//dispose timer



        private void settingsButton_Click(object sender, RoutedEventArgs e)
        {
            //support clicking on screen to hide settingsGrid
            if ((sender as Image) != null && settingsGrid.Visibility == Visibility.Visible || (sender as Image) == null)
            {
                //animate settingsGrid hiding/showing
                settingsGrid.Visibility = Visibility.Visible;
                settingsGridStoryboard.Begin();

                //change visibility of saveButton
                if (settingsGrid.Opacity > 0.5)
                {
                    saveButton.IsEnabled = true;
                    Play();
                }
                else
                {
                    saveButton.IsEnabled = false;
                }
            }
        }//hide/show settingsGrid

        private async void saveButton_Click(object sender, RoutedEventArgs e)
        {
            ShowProgressBar(Localize("saving"));
            await SaveImagesAsGifFileAsync(frameArray);
            HideProgressBar(String.Format("{0} {1}... .", filename, Localize("saved")));
            await Task.Delay(TimeSpan.FromSeconds(1));
            statusTextBlock.Text = String.Format("{0}", Localize("editing"));
        }//save GIF-file

        private void playButton_Click(object sender, RoutedEventArgs e)
        {
            if (animationTimer.IsEnabled)
            {
                Stop();
            }
            else
            {
                Play();
            }
        }//stop/play timer

        private void defaultButton_Click(object sender, RoutedEventArgs e)
        {
            SetButtonColor(filter1);
            lastButtonName = "filter1";
            DefaultSliderValues();
            frameArray = imageProcessor.OriginalArray;
            //Play();
        }//change settings to default

        private async void AddFilter(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!isprocessing)
                {
                    ShowProgressBar(Localize("processing"));
                    await Task.Delay(TimeSpan.FromMilliseconds(100));
                    //show progress bar and create delay to inform user that we saved changes

                    var button = sender as Button;
                    SetButtonColor(button);
                    //refresh buttons` colors

                    bool filterChanged = lastButtonName != button.Name.ToString();

                    if (filterChanged)
                    {
                        lastButtonName = button.Name.ToString();

                        switch (lastButtonName)
                        {
                            case "filter1":
                                frameArray = imageProcessor.OriginalArray;
                                break;

                            case "filter2":
                                frameArray = imageProcessor.GetGreyAndSet();
                                break;

                            case "filter3":
                                frameArray = imageProcessor.GetInvertedAndSet();
                                break;

                            case "filter4":
                                frameArray = imageProcessor.GetChangedArray(-30, 10, true);
                                break;

                            case "filter5":
                                frameArray = imageProcessor.GetChangedArray(-50, 20, true);
                                break;

                            case "filter6":
                                frameArray = imageProcessor.GetChangedArray(-50, 40, true);
                                break;

                            case "filter7":
                                frameArray = imageProcessor.GetChangedArray(-90, 30, true);
                                break;

                            case "filter8":
                                frameArray = imageProcessor.GetChangedArray(30, 30, true);
                                break;

                            case "filter9":
                                frameArray = imageProcessor.GetChangedArray(50, -20, true);
                                break;
                        }

                        if (brightSlider.Value != 0 || contrastSlider.Value != 0)
                        {
                            await RefreshAnimationAsync(true);
                        }
                    }//if choosen filter changed -- set current filter to new filter


                    Play();
                    HideProgressBar(Localize("editing"));
                }
            }
            catch (OutOfMemoryException)
            {
                var dlg = new MessageDialog(Localize("memoryError"));
                await dlg.ShowAsync();
            }
            catch (Exception)
            {
                SetButtonColor(filter1);
                lastButtonName = "filter1";
                DefaultSliderValues();
                frameArray = imageProcessor.OriginalArray;
                Play();
            }
        }//change animation when current filter changes

    }
}