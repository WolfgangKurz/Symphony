using Microsoft.Toolkit.Uwp.Notifications;

using System;
using System.Linq;
using System.Windows.Forms;

using Windows.UI.Notifications;

using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace Symphony.Notification {
	internal static class Program {
		[STAThread]
		static void Main(string[] args) {
			ToastNotificationManagerCompat.OnActivated += toastArgs => {
				new ToastContentBuilder()
					.AddText("안녕하세요!")
					.AddText(string.Join("\n", toastArgs.Argument))
					.Show();
			};

			if (args.Contains("-Schedule")) {
				var tag = args.FirstOrDefault(x => x.StartsWith("-Tag:"))?.Substring(5);
				if (tag == null) {
					Console.WriteLine("Failed to find Tag");
					return;
				}

				var title = args.FirstOrDefault(x => x.StartsWith("-Title:"))?.Substring(7);
				if (title == null) {
					Console.WriteLine("Failed to find Title");
					return;
				}

				var message = args.FirstOrDefault(x => x.StartsWith("-Message:"))?.Substring(9);
				if (message == null) {
					Console.WriteLine("Failed to find Message");
					return;
				}

				var time = args.FirstOrDefault(x => x.StartsWith("-Time:"))?.Substring(6);
				if (time == null) {
					Console.WriteLine("Failed to find Time");
					return;
				}

				if (!DateTime.TryParse(time, out var scheduledTime)) {
					Console.WriteLine("Failed to parse Time");
					return;
				}

				var ticksToWait = scheduledTime.Ticks - DateTime.Now.Ticks;
				if (ticksToWait < 0) {
					Console.WriteLine("Toast time is past than now");
					return;
				}

				var timeToShow = DateTimeOffset.Now.AddTicks(ticksToWait);

				var toast = new ToastContentBuilder()
					.AddArgument("tag", tag)

					.AddText(title)
					.AddText(message)
					.GetToastContent();

				var scheduledToast = new ScheduledToastNotification(toast.GetXml(), timeToShow) {
					Tag = tag,
					Group = "Toast"
				};

				RemoveScheduled(tag); // Remove all same tag schedules first
				ToastNotificationManagerCompat
					.CreateToastNotifier()
					.AddToSchedule(scheduledToast);
			}
			else if (args.Contains("-Cancel")) {
				var tag = args.FirstOrDefault(x => x.StartsWith("-Tag:"))?.Substring(5);
				if (tag == null) {
					Console.WriteLine("Failed to find Tag");
					return;
				}

				RemoveScheduled(tag);
			} else {
				Console.WriteLine("Invalid calling");
			}
		}

		private static void RemoveScheduled(string Tag) {
			var notifier = ToastNotificationManagerCompat.CreateToastNotifier();
			var scheduledToasts = notifier.GetScheduledToastNotifications();

			foreach (var toast in scheduledToasts) {
				if (toast.Tag == Tag)
					notifier.RemoveFromSchedule(toast);
			}
		}
	}
}
