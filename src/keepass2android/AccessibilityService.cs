using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Views.Accessibility;
using Android.Widget;
using KeePassLib;

/*
namespace keepass2android.AutoFill
{
    //<meta-data android:name="android.accessibilityservice" android:resource="@xml/serviceconfig" />
    [Service(Enabled =true, Permission= "android.permission.BIND_ACCESSIBILITY_SERVICE")]
    [IntentFilter(new[] { "android.accessibilityservice.AccessibilityService" })]
    [MetaData("android.accessibilityservice", Resource = "@xml/accserviceconfig")]
    public class AccessibilityService : Android.AccessibilityServices.AccessibilityService
    {
	    private static bool _hasUsedData;
	    const string _logTag = "KP2AAS";
        private const int autoFillNotificationId = 98810;
        private const string androidAppPrefix = "androidapp://";

        public override void OnCreate()
        {
            base.OnCreate();
            Android.Util.Log.Debug(_logTag, "OnCreate Service");
        }

        protected override void OnServiceConnected()
        {
            Android.Util.Log.Debug(_logTag, "service connected");
            base.OnServiceConnected();
        }

        public override void OnAccessibilityEvent(AccessibilityEvent e)
        {
            
            Android.Util.Log.Debug(_logTag, "OnAccEvent");
            if (e.EventType == EventTypes.WindowContentChanged || e.EventType == EventTypes.WindowStateChanged)
            {
                Android.Util.Log.Debug(_logTag, "event: " + e.EventType + ", package = " + e.PackageName);
				if (e.PackageName == "com.android.systemui")
					return; //avoid that the notification is cancelled when pulling down notif drawer
                var root = RootInActiveWindow;
	            int eventWindowId = e.WindowId;
				if ((ExistsNodeOrChildren(root, n => n.WindowId == eventWindowId) && !ExistsNodeOrChildren(root, IsSystemUi)))
                {
					bool cancelNotification = true;

					string url = androidAppPrefix + root.PackageName;

                    if (root.PackageName == "com.android.chrome")
                    {
                        var addressField = root.FindAccessibilityNodeInfosByViewId("com.android.chrome:id/url_bar").FirstOrDefault();
                        UrlFromAddressField(ref url, addressField);

                    }
                    else if (root.PackageName == "com.android.browser")
                    {
                        var addressField = root.FindAccessibilityNodeInfosByViewId("com.android.browser:id/url").FirstOrDefault();
                        UrlFromAddressField(ref url, addressField);
                    }

	                if (ExistsNodeOrChildren(root, IsPasswordField))
                    {

						if ((LastReceivedCredentialsUser != null) && IsSame(GetCredentialsField(PwDefs.UrlField), url))
                        {
							Android.Util.Log.Debug ("KP2AAS", "Filling credentials for " + url);

							List<AccessibilityNodeInfo> emptyPasswordFields = new List<AccessibilityNodeInfo>();
							GetNodeOrChildren(root, IsPasswordField, ref emptyPasswordFields);

	                        List<AccessibilityNodeInfo> allEditTexts = new List<AccessibilityNodeInfo>();
							GetNodeOrChildren(root, IsEditText, ref allEditTexts);

							var usernameEdit = allEditTexts.TakeWhile(edit => (edit.Password == false)).LastOrDefault();

                            FillPassword(url, usernameEdit, emptyPasswordFields);
	                        allEditTexts.Clear();
	                        emptyPasswordFields.Clear();
                        }
                        else
                        {
							Android.Util.Log.Debug ("KP2AAS", "Notif for " + url );
							if (LastReceivedCredentialsUser != null) 
							{
								Android.Util.Log.Debug ("KP2AAS", GetCredentialsField(PwDefs.UrlField));
								Android.Util.Log.Debug ("KP2AAS", url);
							}

                            AskFillPassword(url);
							cancelNotification = false;
                        }

                    }
					if (cancelNotification)
					{
						((NotificationManager)GetSystemService(NotificationService)).Cancel(autoFillNotificationId);
						Android.Util.Log.Debug ("KP2AAS","Cancel notif");
					}
                }

            }
	        GC.Collect();
			Java.Lang.JavaSystem.Gc();

        }


	    private bool IsSystemUi(AccessibilityNodeInfo n)
	    {
		    return (n.ViewIdResourceName != null) && (n.ViewIdResourceName.StartsWith("com.android.systemui"));
	    }

	    private static void UrlFromAddressField(ref string url, AccessibilityNodeInfo addressField)
        {
            if (addressField != null)
            {
                url = addressField.Text;
                if (!url.Contains("://"))
                    url = "http://" + url;
            }
            
        }

		private bool IsSame(string url1, string url2)
		{
			if (url1.StartsWith ("androidapp://"))
				return url1 == url2;
			return KeePassLib.Utility.UrlUtil.GetHost (url1) == KeePassLib.Utility.UrlUtil.GetHost (url2);
		}

        private static bool IsPasswordField(AccessibilityNodeInfo n)
        {
            //if (n.Password) Android.Util.Log.Debug(_logTag, "pwdx with " + (n.Text == null ? "null" : n.Text));
            var res = n.Password && string.IsNullOrEmpty(n.Text);
            // if (n.Password) Android.Util.Log.Debug(_logTag, "pwd with " + n.Text + res);
            return res;
        }
        
        private static bool IsEditText(AccessibilityNodeInfo n)
        {
            //it seems like n.Editable is not a good check as this is false for some fields which are actually editable, at least in tests with Chrome.
            return (n.ClassName != null) && (n.ClassName.Contains("EditText"));
        }

	    private static bool IsNonPasswordEditText(AccessibilityNodeInfo n)
	    {
		    return IsEditText(n) && n.Password == false;
	    }

        private void AskFillPassword(string url)
        {
			
			Intent startKp2aIntent = PackageManager.GetLaunchIntentForPackage(ApplicationContext.PackageName);
	        if (startKp2aIntent != null)
	        {
		        startKp2aIntent.AddCategory(Intent.CategoryLauncher);
		        startKp2aIntent.AddFlags(ActivityFlags.NewTask | ActivityFlags.ClearTask);
		        string taskName = "SearchUrlTask";
				startKp2aIntent.PutExtra("KP2A_APPTASK", taskName); 
				startKp2aIntent.PutExtra("UrlToSearch", url);
			}

	        
            var pending = PendingIntent.GetActivity(this, 0, startKp2aIntent, PendingIntentFlags.UpdateCurrent);
            var targetName = url;
								
            if (url.StartsWith(androidAppPrefix))
            {
                var packageName = url.Substring(androidAppPrefix.Length);
                try
                {
					var appInfo = PackageManager.GetApplicationInfo(packageName, 0);
					targetName = (string) (appInfo != null ? PackageManager.GetApplicationLabel(appInfo) : packageName);
                }
                catch (Exception e)
                {
                    Android.Util.Log.Debug(_logTag, e.ToString());
                    targetName = packageName;
                }
            }
            else
            {
                targetName = KeePassLib.Utility.UrlUtil.GetHost(url);
            }
            

            var builder = new Notification.Builder(this);
            //TODO icon
            //TODO plugin icon
            builder.SetSmallIcon(Resource.Drawable.ic_notify_autofill)
                   .SetContentText(GetString(Resource.String.NotificationContentText, new Java.Lang.Object[] { targetName }))
                   .SetContentTitle(GetString(Resource.String.NotificationTitle))
                   .SetWhen(Java.Lang.JavaSystem.CurrentTimeMillis())
                   .SetVisibility(Android.App.NotificationVisibility.Secret)
                   .SetContentIntent(pending);
            var notificationManager = (NotificationManager)GetSystemService(NotificationService);
            notificationManager.Notify(autoFillNotificationId, builder.Build());
            
        }

        private void FillPassword(string url, AccessibilityNodeInfo usernameEdit, List<AccessibilityNodeInfo> passwordFields)
        {
	        if ((Keepass2android.Kbbridge.KeyboardData.HasData) && (_hasUsedData == false))
	        {
				FillDataInTextField(usernameEdit, LastReceivedCredentialsUser);
				foreach (var pwd in passwordFields)
					FillDataInTextField(pwd, LastReceivedCredentialsPassword);

		        _hasUsedData = true;
	        }


            
            //LookupCredentialsActivity.LastReceivedCredentials = null;
        }

	    public string LastReceivedCredentialsPassword	
	    {
		    get { return GetCredentialsField(PwDefs.PasswordField); }
	    }

	    public string GetCredentialsField(string key)
	    {
		    var field = Keepass2android.Kbbridge.KeyboardData.AvailableFields
			    .Cast<Keepass2android.Kbbridge.StringForTyping>().SingleOrDefault(x => x.Key == key);
		    if (field == null)
			    return null;
		    return field.Value;
	    }

	    public string LastReceivedCredentialsUser	
	    {
			get { return GetCredentialsField(PwDefs.UserNameField); }
	    }

	    private static void FillDataInTextField(AccessibilityNodeInfo edit, string newValue)
	    {
		    if (newValue == null)
			    return;
            Bundle b = new Bundle();
            b.PutString(AccessibilityNodeInfo.ActionArgumentSetTextCharsequence, newValue);
            edit.PerformAction(Android.Views.Accessibility.Action.SetText, b);
        }

        private bool ExistsNodeOrChildren(AccessibilityNodeInfo n, Func<AccessibilityNodeInfo, bool> p)
        {
	        if (n == null) return false;
	        if (p(n))
		        return true;
	        for (int i = 0; i < n.ChildCount; i++)
	        {
		        if (ExistsNodeOrChildren(n.GetChild(i), p))
			        return true;
	        }
	        return false;
        }

        private void GetNodeOrChildren(AccessibilityNodeInfo n, Func<AccessibilityNodeInfo, bool> p, ref List<AccessibilityNodeInfo> result)
        {
	        if (n != null)
            {
	            if (p(n))
		            result.Add(n);
                for (int i = 0; i < n.ChildCount; i++)
                {
	                GetNodeOrChildren(n.GetChild(i), p, ref result);
                }
            }
        }

	    public override void OnInterrupt()
        {
            
        }

        public void OnCancel(IDialogInterface dialog)
        {
            
        }

	    public static void NotifyNewData()
	    {
		    _hasUsedData = false;
	    }
    }
}*/