using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Grocery.App.Views;
using Grocery.Core.Interfaces.Services;
using Grocery.Core.Models;
using System.Collections.ObjectModel;
using System.Text.Json;

namespace Grocery.App.ViewModels
{
    [QueryProperty(nameof(GroceryList), nameof(GroceryList))]
    public partial class GroceryListItemsViewModel : BaseViewModel
    {
        private readonly IGroceryListItemsService _groceryListItemsService;
        private readonly IProductService _productService;
        private readonly IFileSaverService _fileSaverService;
        
        public ObservableCollection<GroceryListItem> MyGroceryListItems { get; set; } = [];
        public ObservableCollection<Product> AvailableProducts { get; set; } = [];
        public ObservableCollection<GroceryListItem> SearchBoodschappenLijstItems { get; set; } = new();

        [ObservableProperty]
        GroceryList groceryList = new(0, "None", DateOnly.MinValue, "", 0);
        [ObservableProperty]
        string myMessage;


        private string _emptyMessage = "Er zijn geen producten meer om toe te voegen";

        // Property voor binding label in 'GroceryListItemsView.xaml'
        public string EmptyMessage
        {
            get { return _emptyMessage; }
            set
            {
                if (_emptyMessage != value)
                {
                    _emptyMessage = value;
                    OnPropertyChanged(nameof(EmptyMessage));
                }
            }
        }

        public GroceryListItemsViewModel(IGroceryListItemsService groceryListItemsService, IProductService productService, IFileSaverService fileSaverService)
        {
            _groceryListItemsService = groceryListItemsService;
            _productService = productService;
            _fileSaverService = fileSaverService;
            Load(groceryList.Id);
        }

        [RelayCommand]  // Maakt onderwater een ICommand property aan voor 'SearchBoodschappenlijst'
        public void SearchBoodschappenlijst(object parameter)
        {
            // Parameter (de zoek opdracht) omzetten naar string
            string query = "";
            if (parameter != null)
            {
                query = (string)parameter;
            }

            SearchBoodschappenLijstItems.Clear();

            foreach (var item in MyGroceryListItems)
            {
                var product = _productService.GetAll().FirstOrDefault(p => p.Id == item.ProductId);

                if (product == null) continue;

                // filter
                if (string.IsNullOrWhiteSpace(query) ||
                    (product.Name != null && product.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    // belangrijk: het bestaande GroceryListItem toevoegen
                    SearchBoodschappenLijstItems.Add(item);
                }
            }

        [RelayCommand]
        public void PerformSearch(object parameter)
        {
            // Checkt of de parameter een lege string is of niet
            string query = (string)(parameter ?? string.Empty);

            // Eerst de AvailableProducts leegmaken als er word getypt in de zoekbalk
            AvailableProducts.Clear();

            // Loop over de producten door gebruik te maken van de .GetAll() functie
            foreach (Product p in _productService.GetAll())
            {
                // Kijkt of het product niet op de lijst staat, zo wel dat door naar de volgende if statement
                bool notOnList = MyGroceryListItems.FirstOrDefault(g => g.ProductId == p.Id) == null;
                if (!notOnList || p.Stock <= 0) continue;

                // kijkt of het product aanwezig is, zo ja toevoegen aan 'AvailableProducts'
                if (string.IsNullOrWhiteSpace(query) ||
                    (p.Name != null && p.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    AvailableProducts.Add(p);
                }
            }

            if (AvailableProducts.Count == 0)
            {   
                // Als er niks is ingevoerd in de searchbar veranderd het bericht dan als een product niet is gevonden
                if (string.IsNullOrWhiteSpace(query))
                    EmptyMessage = "Er zijn geen producten meer om toe te voegen";
                else
                    EmptyMessage = "Producten zijn niet gevonden";
            }
            else
            {
                EmptyMessage = ""; // leeg maken als er resultaten zijn
            }

        }
        
        

        private void Load(int id)
        {
            MyGroceryListItems.Clear();
            foreach (var item in _groceryListItemsService.GetAllOnGroceryListId(id)) MyGroceryListItems.Add(item);
            GetAvailableProducts();

            SearchBoodschappenLijstItems.Clear();
            foreach (var item in MyGroceryListItems)
                SearchBoodschappenLijstItems.Add(item);
        }

        private void GetAvailableProducts()
        {
            AvailableProducts.Clear();
            foreach (Product p in _productService.GetAll())
                if (MyGroceryListItems.FirstOrDefault(g => g.ProductId == p.Id) == null  && p.Stock > 0)
                    AvailableProducts.Add(p);
        }

        partial void OnGroceryListChanged(GroceryList value)
        {
            Load(value.Id);
        }

        [RelayCommand]
        public async Task ChangeColor()
        {
            Dictionary<string, object> paramater = new() { { nameof(GroceryList), GroceryList } };
            await Shell.Current.GoToAsync($"{nameof(ChangeColorView)}?Name={GroceryList.Name}", true, paramater);
        }
        [RelayCommand]
        public void AddProduct(Product product)
        {
            if (product == null) return;
            GroceryListItem item = new(0, GroceryList.Id, product.Id, 1);
            _groceryListItemsService.Add(item);
            product.Stock--;
            _productService.Update(product);
            AvailableProducts.Remove(product);

            // Het GroceryListItem toevoegen aan de zoeklijst zodat het direct zichtbaar is
            SearchBoodschappenLijstItems.Add(item);

            OnGroceryListChanged(GroceryList);
        }

        [RelayCommand]
        public async Task ShareGroceryList(CancellationToken cancellationToken)
        {
            if (GroceryList == null || MyGroceryListItems == null) return;
            string jsonString = JsonSerializer.Serialize(MyGroceryListItems);
            try
            {
                await _fileSaverService.SaveFileAsync("Boodschappen.json", jsonString, cancellationToken);
                await Toast.Make("Boodschappenlijst is opgeslagen.").Show(cancellationToken);
            }
            catch (Exception ex)
            {
                await Toast.Make($"Opslaan mislukt: {ex.Message}").Show(cancellationToken);
            }
        }

    }
}
