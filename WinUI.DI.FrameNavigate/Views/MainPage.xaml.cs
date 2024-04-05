﻿using Microsoft.UI.Xaml.Controls;

using WinUI.DI.FrameNavigate.ViewModels;

namespace WinUI.DI.FrameNavigate.Views;

public sealed partial class MainPage : Page
{
    public MainViewModel ViewModel
    {
        get;
    }

    public MainPage()
    {
        ViewModel = App.GetService<MainViewModel>();
        InitializeComponent();
    }
}
