using System;
using Windows.Phone.UI.Input;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Animation;

namespace GIFEditor
{

    public sealed partial class App : Application
    {
        private TransitionCollection transitions;

        public App()
        {
            this.InitializeComponent();
            this.Suspending += this.OnSuspending;
            HardwareButtons.BackPressed += OnBackPressed;
        }

        private void OnBackPressed(object sender, BackPressedEventArgs e)
        {
            var Frame = Window.Current.Content as Frame;

            if (Frame.CurrentSourcePageType == typeof(Edit))
            {
                e.Handled = true;
                Frame.Navigate(typeof(MainPage));
            }
            else
            if (Frame.CurrentSourcePageType != typeof(MainPage))
            {
                e.Handled = true;
                Frame.GoBack();
            }
        }//support back hardware button press

        protected async override void OnLaunched(LaunchActivatedEventArgs e)
        {
            StatusBar statusBar = StatusBar.GetForCurrentView();
            await statusBar.HideAsync();
            Frame rootFrame = Window.Current.Content as Frame;
            if (rootFrame == null)
            {
                rootFrame = new Frame();
                rootFrame.CacheSize = 1;
                rootFrame.Language = Windows.System.UserProfile.GlobalizationPreferences.Languages[0];
                Window.Current.Content = rootFrame;
            }
            if (rootFrame.Content == null)
            {
                if (rootFrame.ContentTransitions != null)
                {
                    this.transitions = new TransitionCollection();
                    foreach (var c in rootFrame.ContentTransitions)
                    {
                        this.transitions.Add(c);
                    }
                }
                rootFrame.ContentTransitions = null;
                rootFrame.Navigated += this.RootFrame_FirstNavigated;
                if (!rootFrame.Navigate(typeof(MainPage), e.Arguments))
                {
                    throw new Exception("Failed to create initial page");
                }
            }
            Window.Current.Activate();
        }

        private void RootFrame_FirstNavigated(object sender, NavigationEventArgs e)
        {
            var rootFrame = sender as Frame;
            rootFrame.ContentTransitions = this.transitions ?? new TransitionCollection() { new NavigationThemeTransition() };
            rootFrame.Navigated -= this.RootFrame_FirstNavigated;
        }

        private void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            deferral.Complete();
        }

        protected override void OnActivated(IActivatedEventArgs args)
        {
            var root = Window.Current.Content as Frame;
            var currentpage = root.Content as MainPage;
            if (currentpage != null && args is FileOpenPickerContinuationEventArgs)
            {
                currentpage.ContinueFileOpenPicker(args as FileOpenPickerContinuationEventArgs);
            }
        }//go to edit page after choosing files from library

    }
}