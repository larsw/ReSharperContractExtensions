﻿using System.Windows;
using System.Windows.Input;
using JetBrains.Annotations;
using JetBrains.DataFlow;
using JetBrains.ReSharper.Feature.Services.Resources;
using JetBrains.UI.Application.PluginSupport;
using JetBrains.UI.CrossFramework;
using JetBrains.UI.Options;
using ReSharper.ContractExtensions.Settings;

namespace JetBrains.ReSharper.PostfixTemplates.Settings
{
  [OptionsPage(
    id: PID, name: "Contract editor extensions",
    typeofIcon: typeof(ServicesThemedIcons.SurroundTemplate),
    ParentId = PluginsPage.Pid)]
  public sealed partial class PostfixOptionsPage : IOptionsPage
  {
    // ReSharper disable once InconsistentNaming
    private const string PID = "ContractExtensions";

    public PostfixOptionsPage([NotNull] Lifetime lifetime,
      [NotNull] OptionsSettingsSmartContext store)
    {
      InitializeComponent();
      DataContext = new ContractExtensionsOptionsViewModel(lifetime, store);
      Control = this;
    }

    public EitherControl Control { get; private set; }
    public string Id { get { return PID; } }
    public bool OnOk() { return true; }
    public bool ValidatePage() { return true; }

    private void DoubleClickCheck(object sender, RoutedEventArgs e)
    {
      var viewModel = ((FrameworkElement) sender).DataContext as ContractExtensionsOptionsViewModel;
      //if (viewModel != null)
      //{
      //  viewModel.IsChecked = !viewModel.IsChecked;
      //}
    }
  }
}