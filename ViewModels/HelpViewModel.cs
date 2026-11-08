// Lumina Cleaner - Enterprise Windows Disk Cleaner & Optimizer
// Copyright (C) 2026 Pilgrim Software
//
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program. If not, see <https://www.gnu.org/licenses/>.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LuminaCleaner.Models;
using LuminaCleaner.Services;

namespace LuminaCleaner.ViewModels;

public partial class HelpViewModel : ObservableObject
{
    private readonly IHelpService _helpService;

    public ObservableCollection<HelpArticleModel> FilteredArticles { get; } = [];

    [ObservableProperty]
    private HelpArticleModel? _selectedArticle;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedCategory = "All";

    [ObservableProperty]
    private int _resultsCount;

    public HelpViewModel(IHelpService? helpService = null)
    {
        _helpService = helpService ?? new HelpService();
        ApplyFilter();
    }

    [RelayCommand]
    public void SelectArticle(HelpArticleModel? article)
    {
        if (article == null) return;

        foreach (var a in FilteredArticles)
        {
            a.IsSelected = (a.Id == article.Id);
        }

        SelectedArticle = article;
    }

    [RelayCommand]
    public void ClearSearch()
    {
        SearchQuery = string.Empty;
        SelectedCategory = "All";
        ApplyFilter();
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedCategoryChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var matches = _helpService.SearchArticles(SearchQuery, SelectedCategory);

        FilteredArticles.Clear();
        foreach (var m in matches)
        {
            FilteredArticles.Add(m);
        }

        ResultsCount = FilteredArticles.Count;

        // Auto select current or first
        if (SelectedArticle == null || !FilteredArticles.Contains(SelectedArticle))
        {
            SelectedArticle = FilteredArticles.FirstOrDefault();
        }

        if (SelectedArticle != null)
        {
            foreach (var a in FilteredArticles)
            {
                a.IsSelected = (a.Id == SelectedArticle.Id);
            }
        }
    }
}
