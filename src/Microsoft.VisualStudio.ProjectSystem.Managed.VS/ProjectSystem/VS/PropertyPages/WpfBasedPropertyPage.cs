// Licensed to the .NET Foundation under one or more agreements. The .NET Foundation licenses this file to you under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Forms;

namespace Microsoft.VisualStudio.ProjectSystem.VS.PropertyPages
{
    /// <summary>
    /// Hosts a WPF implementation of a property page UI within the WinForms based property page infrastructure.
    /// </summary>
    internal abstract partial class WpfBasedPropertyPage : PropertyPage
    {
        private PropertyPageElementHost? _host;
        private PropertyPageControl? _control;
        private PropertyPageViewModel? _viewModel;

        protected WpfBasedPropertyPage()
        {
            InitializeComponent();
        }

        protected abstract PropertyPageViewModel CreatePropertyPageViewModel();

        protected abstract PropertyPageControl CreatePropertyPageControl();

        protected override async Task OnSetObjectsAsync(bool isClosing)
        {
            if (isClosing)
            {
                _control?.DetachViewModel();
                return;
            }

            //viewModel can be non-null when the configuration is changed.
            _control ??= CreatePropertyPageControl();

            _viewModel = CreatePropertyPageViewModel();
            _viewModel.Project = UnconfiguredProject;
            await _viewModel.InitializeAsync();
            _control.InitializePropertyPage(_viewModel);
        }

        protected override Task<int> OnApplyAsync()
        {
#pragma warning disable VSTHRD110 // Observe result of async calls
            return _control?.ApplyAsync() ?? Task.FromResult((int)HResult.Fail);
#pragma warning restore VSTHRD110 // Observe result of async calls
        }

        protected override Task OnDeactivateAsync()
        {
            if (IsDirty)
            {
                return OnApplyAsync();
            }

            return Task.CompletedTask;
        }

        private void WpfPropertyPage_Load(object sender, EventArgs e)
        {
            SuspendLayout();

            _host = new PropertyPageElementHost
            {
                AutoSize = false,
                Dock = DockStyle.Fill
            };

            _control ??= CreatePropertyPageControl();

            _host.Child = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
                Focusable = false,
                Content = _control
            };

            wpfHostPanel.Dock = DockStyle.Fill;
            wpfHostPanel.Controls.Add(_host);

            ResumeLayout(true);

            _control.StatusChanged += OnControlStatusChanged;
        }

        private void OnControlStatusChanged(object sender, EventArgs e)
        {
            if (_control == null)
            {
                return;
            }

            if (IsDirty != _control.IsDirty)
            {
                IsDirty = _control.IsDirty;
            }
        }

        protected override void Dispose(bool disposing)
        {
            try
            {
                if (disposing)
                {
                    _host?.Dispose();
                }
            }
            finally
            {
                base.Dispose(disposing);
            }
        }
    }
}
