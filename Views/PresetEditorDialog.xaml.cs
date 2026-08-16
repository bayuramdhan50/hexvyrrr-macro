using System;
using System.Windows;
using PbRecoil.Models;

namespace PbRecoil.Views
{
    public partial class PresetEditorDialog : Window
    {
        public WeaponPreset Preset { get; private set; }

        public PresetEditorDialog(WeaponPreset preset)
        {
            InitializeComponent();
            Preset = (WeaponPreset)preset.Clone();

            // Populate Categories
            CmbCategory.ItemsSource = Enum.GetValues(typeof(WeaponCategory));

            // Load Values to Form
            TxtName.Text = Preset.Name;
            CmbCategory.SelectedItem = Preset.Category;
            SliderVertical.Value = Preset.VerticalRecoil;
            SliderHorizontal.Value = Preset.HorizontalRecoil;
            SliderDelay.Value = Preset.DelayMs;
            SliderSmooth.Value = Preset.SmoothStep;
            SliderJitter.Value = Preset.Jitter;
            ChkScopeOnly.IsChecked = Preset.ScopeOnly;
            TxtDescription.Text = Preset.Description;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtName.Text))
            {
                MessageBox.Show("Nama senjata tidak boleh kosong!", "Peringatan", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtName.Focus();
                return;
            }

            Preset.Name = TxtName.Text.Trim();
            Preset.Category = (WeaponCategory)(CmbCategory.SelectedItem ?? WeaponCategory.Custom);
            Preset.VerticalRecoil = (int)SliderVertical.Value;
            Preset.HorizontalRecoil = (int)SliderHorizontal.Value;
            Preset.DelayMs = (int)SliderDelay.Value;
            Preset.SmoothStep = (int)SliderSmooth.Value;
            Preset.Jitter = (int)SliderJitter.Value;
            Preset.ScopeOnly = ChkScopeOnly.IsChecked ?? false;
            Preset.Description = TxtDescription.Text.Trim();
            Preset.IsDefault = false; // Custom created / edited is not default

            DialogResult = true;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
