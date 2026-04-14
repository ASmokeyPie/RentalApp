using Android.App;
using Android.Runtime;

namespace RentalApp;

[Application]
public class MainApplication : MauiApplication
{
	public MainApplication(IntPtr handle, JniHandleOwnership ownership)
		: base(handle, ownership)
	{
      AndroidEnvironment.UnhandledExceptionRaiser += (sender, args) =>                                                                                                                          
      {                                                                                                                                                                                         
          Android.Util.Log.Error("CRASH", args.Exception.ToString());                                                                                                                           
          args.Handled = true;                                                                                                                                                                  
      }; 
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
