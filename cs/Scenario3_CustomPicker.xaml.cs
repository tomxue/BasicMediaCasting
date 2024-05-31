//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// This code is licensed under the MIT License (MIT).
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SDKTemplate;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.Media.Casting;
using Windows.Devices.Enumeration;
using Windows.Media.Playback;
using System.Diagnostics;
using Windows.Media.Core;

namespace BasicMediaCasting
{
    public sealed partial class Scenario3 : Page
    {
        private MainPage rootPage;
        private DeviceWatcher watcher;
        private CastingConnection connection;
        private MediaPlayer mediaPlayer;

        public Scenario3()
        {
            this.InitializeComponent();

            //Create our watcher and have it find casting devices capable of video casting
            watcher = DeviceInformation.CreateWatcher(CastingDevice.GetDeviceSelector(CastingPlaybackTypes.Video));

            //Register for watcher events
            watcher.Added += Watcher_Added;
            watcher.Removed += Watcher_Removed;
            watcher.Stopped += Watcher_Stopped;
            watcher.EnumerationCompleted += Watcher_EnumerationCompleted;

            video.MediaOpened += Video_MediaOpened;
            video.VolumeChanged += Video_VolumeChanged;
            video.MediaEnded += Video_MediaEnded;
            mediaPlayer = new MediaPlayer();
        }

        private void Video_MediaEnded(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{nameof(Video_MediaEnded)}: invoked");
        }

        private void Video_VolumeChanged(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{nameof(Video_VolumeChanged)}: invoked");
        }

        private void Video_MediaOpened(object sender, RoutedEventArgs e)
        {
            Debug.WriteLine($"{nameof(Video_MediaOpened)}: invoked");
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            rootPage = MainPage.Current;
        }

        private async void loadButton_Click(object sender, RoutedEventArgs e)
        {
            //Create a new picker
            FileOpenPicker filePicker = new FileOpenPicker();

            //Add filetype filters.  In this case wmv and mp4.
            filePicker.FileTypeFilter.Add(".wmv");
            filePicker.FileTypeFilter.Add(".mp4");

            //Set picker start location to the video library
            filePicker.SuggestedStartLocation = PickerLocationId.VideosLibrary;

            //Retrieve file from picker
            StorageFile file = await filePicker.PickSingleFileAsync();

            //If we got a file, load it into the media element
            //if (file != null)
            //{
            //    IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
            //    video.SetSource(stream, file.ContentType);
            //    rootPage.NotifyUser("Content Selected", NotifyType.StatusMessage);
            //}

            TimeSpan timeSpan = TimeSpan.FromSeconds(10);
            StartMediaPlayer(timeSpan);
        }

        private void watcherControlButton_Click(object sender, RoutedEventArgs e)
        {
            //If the watcher isn't running, start it up
            if (watcher.Status != DeviceWatcherStatus.Started)
            {
                //clear the list as we're starting the watcher over
                castingDevicesList.Items.Clear();

                //start the watcher
                watcher.Start();

                //update the UI to reflect the watcher's state
                rootPage.NotifyUser("Watcher has been started", NotifyType.StatusMessage);
                watcherControlButton.Content = "Stop Device Watcher";
                progressText.Text = "Searching";
                progressRing.IsActive = true;
            }
            else
            {
                //if the watcher is running, stop the watcher and update UI
                progressText.Text = "";
                watcher.Stop();
            }
        }

        private async void Watcher_Added(DeviceWatcher sender, DeviceInformation args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                //Add each discovered device to our listbox
                CastingDevice addedDevice = await CastingDevice.FromIdAsync(args.Id);
                castingDevicesList.Items.Add(addedDevice);
            });
        }

        private async void Watcher_Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //Remove any removed devices from our listbox
                foreach (CastingDevice currentDevice in castingDevicesList.Items)
                {
                    if (currentDevice.Id == args.Id)
                    {
                        castingDevicesList.Items.Remove(currentDevice);
                        break;
                    }
                }
            });
        }

        private async void Watcher_EnumerationCompleted(DeviceWatcher sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //If enumeration completes, update UI and transition watcher to the stopped state
                rootPage.NotifyUser("Watcher completed enumeration of devices", NotifyType.StatusMessage);
                progressText.Text = "Enumeration Completed";
                watcher.Stop();
            });
        }

        private async void Watcher_Stopped(DeviceWatcher sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //Update UX when the watcher stops
                rootPage.NotifyUser("Watcher has been stopped", NotifyType.StatusMessage);
                watcherControlButton.Content = "Start Device Watcher";
                progressRing.IsActive = false;
            });
        }

        private async void castingDevicesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (castingDevicesList.SelectedItem != null)
            {
                //When a device is selected, first thing we do is stop the watcher so it's search doesn't conflict with streaming
                if (watcher.Status != DeviceWatcherStatus.Stopped)
                {
                    progressText.Text = "";
                    watcher.Stop();
                }

                //Create a new casting connection to the device that's been selected
                connection = ((CastingDevice)castingDevicesList.SelectedItem).CreateCastingConnection();

                //Register for events
                connection.ErrorOccurred += Connection_ErrorOccurred;
                connection.StateChanged += Connection_StateChanged;

                //Cast the loaded video to the selected casting device.
                CastingSource source = mediaPlayer.GetAsCastingSource();
                await connection.RequestStartCastingAsync(source);
            }
        }

        private async void Connection_StateChanged(CastingConnection sender, object args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //Update the UX based on the casting state
                if (sender.State == CastingConnectionState.Connected || sender.State == CastingConnectionState.Rendering)
                {
                    disconnectButton.Visibility = Visibility.Visible;
                    progressText.Text = "Connected";
                    progressRing.IsActive = false;
                }
                else if (sender.State == CastingConnectionState.Disconnected)
                {
                    disconnectButton.Visibility = Visibility.Collapsed;
                    castingDevicesList.SelectedItem = null;
                    progressText.Text = "";
                    progressRing.IsActive = false;
                }
                else if (sender.State == CastingConnectionState.Connecting)
                {
                    disconnectButton.Visibility = Visibility.Collapsed;
                    progressText.Text = "Connecting";
                    progressRing.IsActive = true;
                }
                else
                {
                    //Disconnecting is the remaining state
                    disconnectButton.Visibility = Visibility.Collapsed;
                    progressText.Text = "Disconnecting";
                    progressRing.IsActive = true;
                }
            });
        }

        private async void Connection_ErrorOccurred(CastingConnection sender, CastingConnectionErrorOccurredEventArgs args)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //Clear the selection in the listbox on an error
                rootPage.NotifyUser("Casting Error: " + args.Message, NotifyType.ErrorMessage);
                castingDevicesList.SelectedItem = null;
            });
        }

        private async void disconnectButton_Click(object sender, RoutedEventArgs e)
        {
            if (connection != null)
            {
                //When disconnect is clicked, the casting conneciton is disconnected.  The video should return locally to the media element.
                await connection.DisconnectAsync();
            }
        }

        private void StartMediaPlayer(TimeSpan position)
        {
            mediaPlayer.Pause();
            mediaPlayer.Source = MediaSource.CreateFromUri(new Uri("ms-appx:///1.mp4"));
            mediaPlayer.PlaybackSession.Position = position;
            mediaPlayer.Volume = 0.05;
            mediaPlayer.PlaybackSession.NaturalVideoSizeChanged -= MediaPlayer_PlaybackSession_NaturalVideoSizeChanged;
            mediaPlayer.PlaybackSession.NaturalVideoSizeChanged += MediaPlayer_PlaybackSession_NaturalVideoSizeChanged;
            mediaPlayer.MediaEnded -= MediaPlayer_MediaEnded;
            mediaPlayer.MediaEnded += MediaPlayer_MediaEnded;
            mediaPlayer.MediaOpened -= MediaPlayer_MediaOpened;
            mediaPlayer.MediaOpened += MediaPlayer_MediaOpened;
            mediaPlayer.VolumeChanged -= MediaPlayer_VolumeChanged;
            mediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged;
            mediaPlayer.PlaybackSession.PositionChanged -= PlaybackSession_PositionChanged;
            mediaPlayer.PlaybackSession.PositionChanged += PlaybackSession_PositionChanged;
            mediaPlayer.PlaybackSession.MediaPlayer.VolumeChanged -= MediaPlayer_VolumeChanged2;
            mediaPlayer.PlaybackSession.MediaPlayer.VolumeChanged += MediaPlayer_VolumeChanged2;
            mediaPlayer.Play();
            Debug.WriteLine($"{nameof(StartMediaPlayer)}: from position: {position.TotalSeconds} s");
        }

        private void PlaybackSession_PositionChanged(Windows.Media.Playback.MediaPlaybackSession sender, object args)
        {
            Debug.WriteLine($"{nameof(PlaybackSession_PositionChanged)}: invoked, position: {sender.Position}");
        }

        private void MediaPlayer_VolumeChanged(MediaPlayer sender, object args)
        {
            Debug.WriteLine($"{nameof(MediaPlayer_VolumeChanged)}: invoked, volume; {sender.Volume}");
        }

        private void MediaPlayer_VolumeChanged2(MediaPlayer sender, object args)
        {
            Debug.WriteLine($"{nameof(MediaPlayer_VolumeChanged2)}: invoked");
        }

        private void MediaPlayer_MediaOpened(MediaPlayer sender, object args)
        {
            Debug.WriteLine($"{nameof(MediaPlayer_MediaOpened)}: invoked");
        }

        private void MediaPlayer_MediaEnded(MediaPlayer sender, object args)
        {
            Debug.WriteLine($"{nameof(MediaPlayer_MediaEnded)}: invoked");
        }

        private void MediaPlayer_PlaybackSession_NaturalVideoSizeChanged(MediaPlaybackSession sender, object args)
        {
            Debug.WriteLine($"{nameof(MediaPlayer_PlaybackSession_NaturalVideoSizeChanged)}: invoked");
        }
    }
}
