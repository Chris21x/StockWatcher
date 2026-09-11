using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Windows.Data.Xml.Dom;
using Windows.UI.Notifications;

namespace StockWatcher.Services
{
	/// <summary>
	/// Windows-Toast-Benachrichtigungen für die portable
	/// .NET-Framework-Desktopanwendung.
	///
	/// Registrierung:
	/// - stabile AppUserModelID am Prozess
	/// - Startmenü-Verknüpfung mit derselben AppUserModelID
	/// - ToastNotificationManager.CreateToastNotifier(AppUserModelID)
	///
	/// WinRT wird bewusst typisiert verwendet. Der frühere Reflection-Weg
	/// über Type.InvokeMember() funktioniert bei den WinRT-COM-Objekten nicht,
	/// weil diese kein klassisches IDispatch implementieren.
	/// </summary>
	internal static class WindowsToastNotifier
	{
		private const string AppUserModelId = "StockWatcher.Desktop";
		private const string LimitGroup = "limits";
		private const string StatusGroup = "status";
		private const string TransientGroup = "transient";
		private const string StatusTag = "appstatus";
		private const string TransientTag = "background";

		private static readonly object ToastSyncRoot = new object();
		private sealed class LimitNotificationState
		{
			public int Generation { get; set; }
			public bool PopupPending { get; set; }
			public string EventTitle { get; set; } = "";
			public string EventSecurity { get; set; } = "";
			public string EventDetails { get; set; } = "";
		}

		private static readonly Dictionary<string, ToastNotification> ActiveToasts =
			new Dictionary<string, ToastNotification>(StringComparer.Ordinal);
		private static readonly Dictionary<string, LimitNotificationState> LimitStates =
			new Dictionary<string, LimitNotificationState>(StringComparer.Ordinal);
		private static int _limitGeneration;
		private static bool _shortcutPrepared;

		public static void InitializeProcessIdentity()
		{
			try
			{
				SetCurrentProcessExplicitAppUserModelID(AppUserModelId);
				EnsureStartMenuShortcut();
			}
			catch
			{
				// Falls WinRT/Shell nicht verfügbar ist, bleibt beim neuen Alarm
				// der bestehende NotifyIcon-Balloon als Fallback verfügbar.
			}
		}

		/// <summary>
		/// Zeigt einen neuen Limit-Toast oder ersetzt einen vorhandenen Eintrag.
		/// suppressPopup=true aktualisiert nur das Benachrichtigungszentrum.
		/// </summary>
		/// <summary>
		/// Zeigt einen neuen Alarm als sichtbaren Popup-Toast. Nach dem Ende des
		/// Popups wird derselbe Tag lautlos durch den separat formatierten
		/// Ereignis-Eintrag ersetzt.
		/// </summary>
		public static bool TryShowLimitPopup(
			string tag,
			string toastTitle,
			string toastSecurity,
			string toastLimit,
			string toastCurrent,
			string eventTitle,
			string eventSecurity,
			string eventDetails)
		{
			string normalizedTag = NormalizeTag(tag);
			string normalizedGroup = NormalizeGroup(LimitGroup);
			int generation;

			lock (ToastSyncRoot)
			{
				generation = ++_limitGeneration;
				LimitStates[normalizedTag] = new LimitNotificationState
				{
					Generation = generation,
					PopupPending = true,
					EventTitle = eventTitle ?? "",
					EventSecurity = eventSecurity ?? "",
					EventDetails = eventDetails ?? ""
				};
			}

			try
			{
				EnsureStartMenuShortcut();

				XmlDocument toastXml =
					ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText02);

				XmlNodeList textNodes = toastXml.GetElementsByTagName("text");
				if (textNodes == null || textNodes.Length < 2)
					return false;

				string body =
					$"{toastSecurity}{Environment.NewLine}{Environment.NewLine}" +
					$"{toastLimit}{Environment.NewLine}{toastCurrent}";

				textNodes[0].AppendChild(toastXml.CreateTextNode(toastTitle ?? ""));
				textNodes[1].AppendChild(toastXml.CreateTextNode(body));

				var toast = new ToastNotification(toastXml)
				{
					Tag = normalizedTag,
					Group = normalizedGroup,
					ExpirationTime = DateTimeOffset.Now.AddHours(12),
					SuppressPopup = false
				};

				toast.Dismissed += (sender, args) =>
					HandleLimitPopupDismissed(normalizedTag, generation);

				ToastNotifier toastNotifier =
					ToastNotificationManager.CreateToastNotifier(AppUserModelId);
				toastNotifier.Show(toast);

				lock (ToastSyncRoot)
				{
					ActiveToasts[GetActiveToastKey(normalizedGroup, normalizedTag)] = toast;
				}

				return true;
			}
			catch (Exception ex)
			{
				lock (ToastSyncRoot)
				{
					if (LimitStates.TryGetValue(normalizedTag, out LimitNotificationState state) &&
						state.Generation == generation)
					{
						LimitStates.Remove(normalizedTag);
					}
				}

				Debug.WriteLine($"[Toast] {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Aktualisiert nur den Ereignis-Eintrag eines bereits ausgelösten Alarms.
		/// Solange dessen Popup noch sichtbar ist, werden lediglich die neuesten
		/// Ereignisdaten vorgemerkt und erst nach Dismissed geschrieben.
		/// </summary>
		public static bool TryRefreshLimitEvent(
			string tag,
			string eventTitle,
			string eventSecurity,
			string eventDetails)
		{
			string normalizedTag = NormalizeTag(tag);
			bool popupPending;

			lock (ToastSyncRoot)
			{
				if (!LimitStates.TryGetValue(normalizedTag, out LimitNotificationState state))
				{
					state = new LimitNotificationState
					{
						Generation = ++_limitGeneration,
						PopupPending = false
					};
					LimitStates[normalizedTag] = state;
				}

				state.EventTitle = eventTitle ?? "";
				state.EventSecurity = eventSecurity ?? "";
				state.EventDetails = eventDetails ?? "";
				popupPending = state.PopupPending;
			}

			if (popupPending)
				return true;

			return TryShowLimitEventCore(
				normalizedTag,
				eventTitle,
				eventSecurity,
				eventDetails);
		}

		private static void HandleLimitPopupDismissed(string normalizedTag, int generation)
		{
			string eventTitle;
			string eventSecurity;
			string eventDetails;

			lock (ToastSyncRoot)
			{
				if (!LimitStates.TryGetValue(normalizedTag, out LimitNotificationState state) ||
					state.Generation != generation)
				{
					return;
				}

				state.PopupPending = false;
				eventTitle = state.EventTitle;
				eventSecurity = state.EventSecurity;
				eventDetails = state.EventDetails;
			}

			TryShowLimitEventCore(
				normalizedTag,
				eventTitle,
				eventSecurity,
				eventDetails);
		}

		private static bool TryShowLimitEventCore(
			string normalizedTag,
			string eventTitle,
			string eventSecurity,
			string eventDetails)
		{
			try
			{
				EnsureStartMenuShortcut();

				XmlDocument toastXml = new XmlDocument();
				toastXml.LoadXml(
					"<toast>" +
					"<visual>" +
					"<binding template=\"ToastGeneric\">" +
					"<text hint-maxLines=\"1\"></text>" +
					"<text hint-maxLines=\"2\"></text>" +
					"<text hint-maxLines=\"2\"></text>" +
					"</binding>" +
					"</visual>" +
					"</toast>");

				XmlNodeList textNodes = toastXml.GetElementsByTagName("text");
				if (textNodes == null || textNodes.Length < 3)
					return false;

				textNodes[0].AppendChild(toastXml.CreateTextNode(eventTitle ?? ""));

				// Windows erlaubt für die beiden Beschreibungselemente zusammen
				// maximal vier sichtbare Zeilen. Genau diese vier werden genutzt:
				// Valor + Leerzeile sowie Limit + Ist-Wert.
				textNodes[1].AppendChild(
					toastXml.CreateTextNode(
						(eventSecurity ?? "") + Environment.NewLine + "\u00A0"));
				textNodes[2].AppendChild(
					toastXml.CreateTextNode(eventDetails ?? ""));

				string normalizedGroup = NormalizeGroup(LimitGroup);
				var toast = new ToastNotification(toastXml)
				{
					Tag = normalizedTag,
					Group = normalizedGroup,
					ExpirationTime = DateTimeOffset.Now.AddHours(12),
					SuppressPopup = true
				};

				ToastNotifier toastNotifier =
					ToastNotificationManager.CreateToastNotifier(AppUserModelId);
				toastNotifier.Show(toast);

				lock (ToastSyncRoot)
				{
					ActiveToasts[GetActiveToastKey(normalizedGroup, normalizedTag)] = toast;
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[ToastHistory] {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		/// <summary>
		/// Schreibt den aktuellen Programmstatus ausschließlich in die
		/// Windows-Ereignisliste / das Benachrichtigungszentrum.
		/// </summary>
		public static bool TryShowStatus(string title, string message)
		{
			return TryShowSingleLineCore(
				StatusTag,
				StatusGroup,
				message,
				suppressPopup: true,
				expirationTime: null);
		}

		/// <summary>
		/// Kurzer Hinweis beim Schließen des Hauptfensters in den Tray.
		/// ExpirationTime ist bewusst 1 Sekunde: Windows bietet keine eigene
		/// millisekundengenaue Popup-Dauer unabhängig von der Gültigkeit.
		/// </summary>
		public static bool TryShowTransient(string message)
		{
			return TryShowSingleLineCore(
				TransientTag,
				TransientGroup,
				message,
				suppressPopup: false,
				expirationTime: DateTimeOffset.Now.AddSeconds(1));
		}

		public static bool TryRemoveLimit(string tag)
		{
			string normalizedTag = NormalizeTag(tag);
			lock (ToastSyncRoot)
			{
				LimitStates.Remove(normalizedTag);
			}

			return TryRemoveCore(normalizedTag, LimitGroup);
		}

		public static bool TryRemoveStatus()
		{
			return TryRemoveCore(StatusTag, StatusGroup);
		}

		public static bool TryClearLimits()
		{
			try
			{
				try
				{
					ToastNotificationManager.History.RemoveGroup(
						LimitGroup,
						AppUserModelId);
				}
				catch
				{
					ToastNotificationManager.History.RemoveGroup(LimitGroup);
				}

				lock (ToastSyncRoot)
				{
					LimitStates.Clear();

					string prefix = LimitGroup + "|";
					var keysToRemove = new List<string>();
					foreach (string key in ActiveToasts.Keys)
					{
						if (key.StartsWith(prefix, StringComparison.Ordinal))
							keysToRemove.Add(key);
					}

					foreach (string key in keysToRemove)
						ActiveToasts.Remove(key);
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[ToastHistory] {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		private static bool TryShowSingleLineCore(
			string tag,
			string group,
			string message,
			bool suppressPopup,
			DateTimeOffset? expirationTime)
		{
			try
			{
				EnsureStartMenuShortcut();

				XmlDocument toastXml =
					ToastNotificationManager.GetTemplateContent(ToastTemplateType.ToastText01);

				XmlNodeList textNodes = toastXml.GetElementsByTagName("text");
				if (textNodes == null || textNodes.Length < 1)
					return false;

				textNodes[0].AppendChild(toastXml.CreateTextNode(message ?? ""));

				string normalizedTag = NormalizeTag(tag);
				string normalizedGroup = NormalizeGroup(group);

				var toast = new ToastNotification(toastXml)
				{
					Tag = normalizedTag,
					Group = normalizedGroup,
					SuppressPopup = suppressPopup
				};

				if (expirationTime.HasValue)
					toast.ExpirationTime = expirationTime.Value;

				ToastNotifier notifier =
					ToastNotificationManager.CreateToastNotifier(AppUserModelId);
				notifier.Show(toast);

				lock (ToastSyncRoot)
				{
					ActiveToasts[GetActiveToastKey(normalizedGroup, normalizedTag)] = toast;
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[Toast] {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}


		private static bool TryRemoveCore(string tag, string group)
		{
			try
			{
				string normalizedTag = NormalizeTag(tag);
				string normalizedGroup = NormalizeGroup(group);

				try
				{
					ToastNotificationManager.History.Remove(
						normalizedTag,
						normalizedGroup,
						AppUserModelId);
				}
				catch
				{
					ToastNotificationManager.History.Remove(
						normalizedTag,
						normalizedGroup);
				}

				lock (ToastSyncRoot)
				{
					ActiveToasts.Remove(GetActiveToastKey(normalizedGroup, normalizedTag));
				}

				return true;
			}
			catch (Exception ex)
			{
				Debug.WriteLine($"[ToastHistory] {ex.GetType().Name}: {ex.Message}");
				return false;
			}
		}

		private static string GetActiveToastKey(string group, string tag)
		{
			return group + "|" + tag;
		}

		private static string NormalizeGroup(string group)
		{
			string value = (group ?? "").Trim();
			if (value.Length == 0)
				throw new ArgumentException("Toast group must not be empty.", nameof(group));

			return value.Length <= 16 ? value : value.Substring(0, 16);
		}

		private static string NormalizeTag(string tag)
		{
			string value = (tag ?? "").Trim();
			if (value.Length == 0)
				throw new ArgumentException("Toast tag must not be empty.", nameof(tag));

			return value.Length <= 16 ? value : value.Substring(0, 16);
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
				ThrowOnFailure(shellLink.SetArguments(""));
				ThrowOnFailure(shellLink.SetWorkingDirectory(workingDirectory));
				ThrowOnFailure(shellLink.SetDescription("StockWatcher"));
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
