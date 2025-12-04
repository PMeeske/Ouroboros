namespace Ouroboros.Android;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();

		// Direct navigation to MainPage - bypass Shell to fix CLI not showing
		MainPage = new NavigationPage(new MainPage());
	}
}
