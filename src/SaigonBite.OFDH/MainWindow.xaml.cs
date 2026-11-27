using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SaigonBite.OFDH.Views.UC14;
using SaigonBite.OFDH.Views.UC1;

namespace SaigonBite.OFDH;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        NavigateTo("UC3");
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender == BtnUc1) NavigateTo("UC1");
        else if (sender == BtnUc3) NavigateTo("UC3");
        else if (sender == BtnUc8) NavigateTo("UC8");
        else if (sender == BtnUc10) NavigateTo("UC10");
        else if (sender == BtnUc14) NavigateTo("UC14");
        else if (sender == BtnUc16) NavigateTo("UC16");
    }

    public void NavigateTo(string ucKey)
    {
        var inactiveBg = (Brush)FindResource("SidebarBtnBgBrush");
        var activeBg = (Brush)FindResource("PrimaryOrangeBrush");

        foreach (var btn in new[] { BtnUc1, BtnUc3, BtnUc8, BtnUc10, BtnUc14, BtnUc16 })
        {
            btn.Background = inactiveBg;
            btn.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
        }

        switch (ucKey)
        {
            case "UC1":
                SetActiveNav(BtnUc1, activeBg, "Customer Registration & Addresses", "Role: Customer (Guest / Tourist)");
                MainContent.Content = new RegistrationAddressView();
                break;
            case "UC3":
                SetActiveNav(BtnUc3, activeBg, "Place an Order — Checkout", "Role: Customer | CUST-001");
                MainContent.Content = BuildPlaceholder("UC3 — Ready for OrderCheckoutView");
                break;
            case "UC8":
                SetActiveNav(BtnUc8, activeBg, "Kitchen Display — Order Preparation", "Role: Kitchen Staff | HUB-D3");
                MainContent.Content = BuildPlaceholder("UC8 — Assigned to Ly Tin Hoa (524H0015)");
                break;
            case "UC10":
                SetActiveNav(BtnUc10, activeBg, "Shipper Delivery — Order Fulfillment", "Role: Shipper | SHP-01");
                MainContent.Content = BuildPlaceholder("UC10 — Assigned to Ha Du Tuan (524H0134)");
                break;
            case "UC14":
                SetActiveNav(BtnUc14, activeBg, "Brand & Menu Catalog Management", "Role: Admin");
                MainContent.Content = new BrandMenuAdminView();
                break;
            case "UC16":
                SetActiveNav(BtnUc16, activeBg, "Management & KPI Reports", "Role: Operations Manager");
                MainContent.Content = BuildPlaceholder("UC16 — Assigned to Ly Tin Hoa (524H0015)");
                break;
        }
    }

    private void SetActiveNav(Button activeBtn, Brush activeBg, string title, string role)
    {
        activeBtn.Background = activeBg;
        activeBtn.Foreground = Brushes.White;
        TxtHeaderTitle.Text = title;
        TxtRoleBadge.Text = role;
    }

    private static UIElement BuildPlaceholder(string message)
    {
        return new Border
        {
            Background = Brushes.White,
            BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Child = new TextBlock
            {
                Text = message,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            }
        };
    }
}
