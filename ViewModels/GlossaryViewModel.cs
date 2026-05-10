using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Computer_networks.Data;
using Computer_networks.Models;
using FuzzySharp;

namespace Computer_networks.ViewModels
{
    public class GlossaryViewModel : INotifyPropertyChanged
    {
        // Коллекция терминов
        public ObservableCollection<GlossaryTerm> Terms { get; set; }

        // Текст поиска
        private string _searchText;
        public string SearchText
        {
            get { return _searchText; }
            set
            {
                _searchText = value;
                OnPropertyChanged(nameof(SearchText));
                SearchTerms();
            }
        }

        // Количество терминов
        private int _termsCount;
        public int TermsCount
        {
            get { return _termsCount; }
            set
            {
                _termsCount = value;
                OnPropertyChanged(nameof(TermsCount));
            }
        }

        // Выбранный термин
        private GlossaryTerm _selectedTerm;
        public GlossaryTerm SelectedTerm
        {
            get { return _selectedTerm; }
            set
            {
                _selectedTerm = value;
                OnPropertyChanged(nameof(SelectedTerm));
            }
        }

        // Команды
        public ICommand CopyTermCommand { get; set; }
        public ICommand CloseCommand { get; set; }

        public GlossaryViewModel()
        {
            Terms = new ObservableCollection<GlossaryTerm>();
            LoadTerms();

            CopyTermCommand = new RelayCommand(CopyTermToClipboard);
            CloseCommand = new RelayCommand(CloseWindow);
        }

        // Загрузка ВСЕХ терминов (без фильтра по курсу)
        private void LoadTerms()
        {
            try
            {
                // 👇 null = загружаем все термины (все курсы)
                var terms = SqlDataAccess.GetAllGlossaryTerms(null);
                Terms.Clear();
                foreach (var term in terms)
                {
                    Terms.Add(term);
                }
                TermsCount = Terms.Count;
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Ошибка загрузки терминов: {ex.Message}");
            }
        }


// Поиск по ВСЕМ терминам (с поддержкой опечаток)
private void SearchTerms()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                LoadTerms();
                return;
            }

            // Получаем все термины из БД
            var allTerms = SqlDataAccess.GetAllGlossaryTerms(null);

            // Если терминов мало или поисковый запрос короткий - используем обычный поиск
            if (allTerms.Count < 10 || SearchText.Length < 3)
            {
                var exactTerms = SqlDataAccess.SearchGlossaryTerms(SearchText, null);
                UpdateTermsList(exactTerms);
                return;
            }

            // Нечёткий поиск с порогом 60% совпадения
            var scoredTerms = allTerms
                .Select(term => new
                {
                    Term = term,
                    // Оцениваем совпадение для термина и определения
                    TermScore = Fuzz.PartialRatio(SearchText, term.Term),
                    DefScore = Fuzz.PartialRatio(SearchText, term.Definition)
                })
                .Select(x => new
                {
                    x.Term,
                    // Берём максимальный балл из двух
                    Score = Math.Max(x.TermScore, x.DefScore)
                })
                .Where(x => x.Score >= 60) // Порог совпадения 60%
                .OrderByDescending(x => x.Score)
                .Select(x => x.Term)
                .ToList();

            // Обновляем список
            Terms.Clear();
            foreach (var term in scoredTerms)
            {
                Terms.Add(term);
            }
            TermsCount = Terms.Count;

            // Если ничего не нашли, показываем подсказку
            if (TermsCount == 0)
            {
                // Можно добавить в XAML текст с подсказкой
                System.Windows.MessageBox.Show(
                    $"По запросу \"{SearchText}\" ничего не найдено.\nПопробуйте другой запрос.",
                    "Результаты поиска",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Ошибка поиска: {ex.Message}");
        }
    }

    private void UpdateTermsList(List<GlossaryTerm> terms)
    {
        Terms.Clear();
        foreach (var term in terms)
        {
            Terms.Add(term);
        }
        TermsCount = Terms.Count;
    }

    // Копирование термина
    private void CopyTermToClipboard(object parameter)
        {
            if (SelectedTerm != null)
            {
                string copyText = $"{SelectedTerm.Term} - {SelectedTerm.Definition}";
                System.Windows.Clipboard.SetText(copyText);
                System.Windows.MessageBox.Show($"Термин \"{SelectedTerm.Term}\" скопирован в буфер обмена!",
                    "Успешно", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            }
            else
            {
                System.Windows.MessageBox.Show("Выберите термин для копирования!",
                    "Внимание", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Warning);
            }
        }

        // Закрытие окна
        private void CloseWindow(object parameter)
        {
            System.Windows.Application.Current.Windows
                .OfType<System.Windows.Window>()
                .FirstOrDefault(w => w is Views.GlossaryWindow)
                ?.Close();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}