using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Playnite.SDK;
using PlayniteUtilities;
using RomManage.Enums;

namespace RomManager
{
    public partial class ComparisonOptions : UserControl, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public List<Tuple<GameListSource, string>> GameListSources { get; }
        public List<Tuple<ComparisonField, string>> ComparisonFields { get; }

        private GameListSource selectedUsingSourceList = GameListSource.SelectedGames;
        private GameListSource selectedAgainstSourceList = GameListSource.AllGames;
        private ComparisonField comparisonField = ComparisonField.RomFileNameWithoutExtension;

        public GameListSource SelectedUsingSourceList { get => selectedUsingSourceList; set => SetValue(ref selectedUsingSourceList, value); }
        public GameListSource SelectedAgainstSourceList { get => selectedAgainstSourceList; set => SetValue(ref selectedAgainstSourceList, value); }
        public ComparisonField SelectedComparisonField { get => comparisonField; set => SetValue(ref comparisonField, value); }

        public ICommand DoComparisonCommand { get; }

        public ComparisonOptions()
        {
            this.GameListSources = GeneralHelpers.GetEnumValues<GameListSource>()
                .Select(e => Tuple.Create(e, GeneralHelpers.HumanizeVariable(Enum.GetName(typeof(GameListSource), e))))
                .ToList();
            this.ComparisonFields = GeneralHelpers.GetEnumValues<ComparisonField>()
                .Select(e => Tuple.Create(e, GeneralHelpers.HumanizeVariable(Enum.GetName(typeof(ComparisonField), e))))
                .ToList();
            this.DoComparisonCommand = new RelayCommand(DoComparison);

            this.DataContext = this;

            InitializeComponent();
        }

        private void DoComparison()
        {
            var window = Window.GetWindow(this);

            window.DialogResult = true;
            window.Close();
        }

        #region INotifyPropertyChanged

        protected void OnPropertyChanged([CallerMemberName] string name = null)
            => this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected void SetValue<T>(ref T property, T value, [CallerMemberName] string propertyName = null)
        {
            property = value;
            OnPropertyChanged(propertyName);
        }

        #endregion INotifyPropertyChanged
    }
}