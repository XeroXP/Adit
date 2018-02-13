﻿using Adit.Code.Client;
using Adit.Code.Shared;
using Adit.Code.Viewer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Adit.Pages
{
    /// <summary>
    /// Interaction logic for ViewerMain.xaml
    /// </summary>
    public partial class Viewer : Page
    {
        public static Viewer Current { get; set; }
        public ViewerSurface DrawingSurface { get; private set; }
        public InputHandler InputHandler { get; private set; }

        public Viewer()
        {
            InitializeComponent();
            Current = this;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            if (Config.Current.IsClientAutoConnectEnabled)
            {
                stackServerInfo.Visibility = Visibility.Collapsed;
            }
            DrawingSurface = new ViewerSurface();
            InputHandler = new InputHandler(DrawingSurface);
            viewerFrame.Content = DrawingSurface;
            RefreshUI();
            if (AditViewer.IsConnected)
            {
                AditViewer.RequestFullscreen = true;
            }
        }
        public void DrawImageCall(Point startDrawingPoint, byte[] imageBytes)
        {
            this.Dispatcher.Invoke(() =>
                {
                    try
                    {
                        DrawingSurface.DrawImage(startDrawingPoint, imageBytes);
                    }
                    finally
                    {
                        AditViewer.SocketMessageHandler.SendImageRequest();
                    }
                }
            );
        }

        private async void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textSessionID.Text))
            {
                MessageBox.Show("Session ID is required.", "Session ID Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            controlsFrame.Visibility = Visibility.Collapsed;
            await AditViewer.Connect(textSessionID.Text.Trim());
            RefreshUI();
        }

        private void RefreshUI()
        {
            if (AditViewer.IsConnected)
            {
                controlsFrame.Visibility = Visibility.Collapsed;
                viewerFrame.Visibility = Visibility.Visible;
            }
            else
            {
                controlsFrame.Visibility = Visibility.Visible;
                viewerFrame.Visibility = Visibility.Collapsed;
            }

        }
        // To refresh UI from other threads.
        public void RefreshUICall()
        {
            this.Dispatcher.Invoke(() => RefreshUI());
        }
    }
}
