using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace StockWatcher.Services
{
	/// <summary>
	/// Windows-Toast-Benachrichtigungen für die klassische, nicht paketierte
	/// .NET-Framework-Desktopanwendung.
	///
	/// Es werden bewusst keine zusätzlichen NuGet-Abhängigkeiten benötigt.
	/// Für Desktop-Toasts registriert sich StockWatcher per-user über eine
	/// Startmenü-Verknüpfung mit stabiler AppUserModelID.
	/// </summary>
	internal static class WindowsToastNotifier
	{
		private const string AppUserModelId = "StockWatcher.Desktop";
		private static bool _shortcutPrepared;

		public static void InitializeProcessIdentity()
		{
			try
			{
				SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
			}
			catch
			{
				// Auf Systemen ohne passende Shell-Unterstützung bleibt später
				// der bestehende NotifyIcon-Balloon als Fallback verfügbar.
			}
		}

		public static bool TryShow(string title, string subtitle, string body)
		{
			try
			{
				EnsureStartMenuShortcut();

				string xml =
					"<toast duration=\"long\">" +
					"<visual><binding template=\"ToastGeneric\">" +
					$"<text>{EscapeXml(title)}</text>" +
					$"<text>{EscapeXml(subtitle)}</text>" +
					$"<text>{EscapeXml(body)}</text>" +
					"</binding></visual>" +
					"</toast>";

				Type xmlDocumentType = Type.GetType(
					"Windows.Data.Xml.Dom.XmlDocument, Windows.Data, ContentType=WindowsRuntime",
					throwOnError: false);
				Type toastNotificationType = Type.GetType(
					"Windows.UI.Notifications.ToastNotification, Windows.UI.Notifications, ContentType=WindowsRuntime",
					throwOnError: false);
				Type toastManagerType = Type.GetType(
					"Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType=WindowsRuntime",
					throwOnError: false);

				if (xmlDocumentType == null || toastNotificationType == null || toastManagerType == null)
					return false;

				object xmlDocument = Activator.CreateInstance(xmlDocumentType);
				xmlDocumentType.InvokeMember(
					"LoadXml",
					BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
					null,
					xmlDocument,
					new object[] { xml });

				object toast = Activator.CreateInstance(
					toastNotificationType,
					new object[] { xmlDocument });

				object notifier = toastManagerType.InvokeMember(
					"CreateToastNotifier",
					BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Static,
					null,
					null,
					new object[] { AppUserModelId });

				if (notifier == null || toast == null)
					return false;

				notifier.GetType().InvokeMember(
					"Show",
					BindingFlags.InvokeMethod | BindingFlags.Public | BindingFlags.Instance,
					null,
					notifier,
					new object[] { toast });

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Toast] {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		private static void EnsureStartMenuShortcut()
		{
			if (_shortcutPrepared)
				return;

			string startMenu = Environment.GetFolderPath(Environment.SpecialFolder.StartMenu);
			if (string.IsNullOrWhiteSpace(startMenu))
				throw new InvalidOperationException("Windows Start Menu path is unavailable.");

			string shortcutDirectory = Path.Combine(startMenu, "Programs", "StockWatcher");
			string shortcutPath = Path.Combine(shortcutDirectory, "StockWatcher.lnk");
			string exePath = Application.ExecutablePath;
			string workingDirectory = Path.GetDirectoryName(exePath) ?? "";

			Directory.CreateDirectory(shortcutDirectory);

			object shellLinkObject = null;
			PropVariant appIdValue = default(PropVariant);
			try
			{
				shellLinkObject = new ShellLink();
				var shellLink = (IShellLinkW)shellLinkObject;

				ThrowOnFailure(shellLink.SetPath(exePath));
				ThrowOnFailure(shellLink.SetWorkingDirectory(workingDirectory));
				ThrowOnFailure(shellLink.SetDescription("Stock Watcher"));
				ThrowOnFailure(shellLink.SetIconLocation(exePath, 0));
				ThrowOnFailure(shellLink.SetShowCmd(1));

				var propertyStore = (IPropertyStore)shellLinkObject;
				PropertyKey appIdKey = new PropertyKey(
					new Guid("9F4C2855-9F79-4B39-A8D0-E1D42DE1D5F3"),
					5);

				appIdValue = PropVariant.FromString(AppUserModelId);
				ThrowOnFailure(propertyStore.SetValue(ref appIdKey, ref appIdValue));
				ThrowOnFailure(propertyStore.Commit());

				var persistFile = (IPersistFile)shellLinkObject;
				ThrowOnFailure(persistFile.Save(shortcutPath, true));

				_shortcutPrepared = true;
			}
			finally
			{
				appIdValue.Dispose();

				if (shellLinkObject != null && Marshal.IsComObject(shellLinkObject))
					Marshal.FinalReleaseComObject(shellLinkObject);
			}
		}

		private static string EscapeXml(string value)
		{
			return System.Security.SecurityElement.Escape(value ?? "") ?? "";
		}

		private static void ThrowOnFailure(int hResult)
		{
			if (hResult < 0)
				Marshal.ThrowExceptionForHR(hResult);
		}

		[DllImport("shell32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
		private static extern int SetCurrentProcessExplicitAppUserModelID(
			[MarshalAs(UnmanagedType.LPWStr)] string appID);

		[ComImport]
		[Guid("00021401-0000-0000-C000-000000000046")]
		private class ShellLink
		{
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("000214F9-0000-0000-C000-000000000046")]
		private interface IShellLinkW
		{
			[PreserveSig] int GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
			[PreserveSig] int GetIDList(out IntPtr ppidl);
			[PreserveSig] int SetIDList(IntPtr pidl);
			[PreserveSig] int GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
			[PreserveSig] int SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
			[PreserveSig] int GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
			[PreserveSig] int SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
			[PreserveSig] int GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
			[PreserveSig] int SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
			[PreserveSig] int GetHotkey(out short pwHotkey);
			[PreserveSig] int SetHotkey(short wHotkey);
			[PreserveSig] int GetShowCmd(out int piShowCmd);
			[PreserveSig] int SetShowCmd(int iShowCmd);
			[PreserveSig] int GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
			[PreserveSig] int SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
			[PreserveSig] int SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
			[PreserveSig] int Resolve(IntPtr hwnd, uint fFlags);
			[PreserveSig] int SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
		private interface IPropertyStore
		{
			[PreserveSig] int GetCount(out uint cProps);
			[PreserveSig] int GetAt(uint iProp, out PropertyKey pkey);
			[PreserveSig] int GetValue(ref PropertyKey key, out PropVariant pv);
			[PreserveSig] int SetValue(ref PropertyKey key, ref PropVariant pv);
			[PreserveSig] int Commit();
		}

		[ComImport]
		[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
		[Guid("0000010B-0000-0000-C000-000000000046")]
		private interface IPersistFile
		{
			[PreserveSig] int GetClassID(out Guid pClassID);
			[PreserveSig] int IsDirty();
			[PreserveSig] int Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, uint dwMode);
			[PreserveSig] int Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, bool fRemember);
			[PreserveSig] int SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
			[PreserveSig] int GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
		}

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		private struct PropertyKey
		{
			public Guid FormatId;
			public uint PropertyId;

			public PropertyKey(Guid formatId, uint propertyId)
			{
				FormatId = formatId;
				PropertyId = propertyId;
			}
		}

		[StructLayout(LayoutKind.Explicit)]
		private struct PropVariant : IDisposable
		{
			[FieldOffset(0)]
			private ushort _valueType;

			[FieldOffset(8)]
			private IntPtr _pointerValue;

			public static PropVariant FromString(string value)
			{
				return new PropVariant
				{
					_valueType = (ushort)VarEnum.VT_LPWSTR,
					_pointerValue = Marshal.StringToCoTaskMemUni(value ?? "")
				};
			}

			public void Dispose()
			{
				PropVariantClear(ref this);
			}
		}

		[DllImport("ole32.dll")]
		private static extern int PropVariantClear(ref PropVariant pvar);
	}
}
