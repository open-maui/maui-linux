using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Hosting;
using Microsoft.Maui.Hosting;
using Microsoft.Maui.Platform.Linux.Services;
using SkiaSharp;

namespace Microsoft.Maui.Platform.Linux.Hosting;

public static class LinuxProgramHost
{
	public static void Run<TApp>(string[] args) where TApp : class, IApplication, new()
	{
		Run<TApp>(args, null);
	}

	public static void Run<TApp>(string[] args, Action<MauiAppBuilder>? configure) where TApp : class, IApplication, new()
	{
		MauiAppBuilder val = MauiApp.CreateBuilder(true);
		val.UseLinux();
		configure?.Invoke(val);
		AppHostBuilderExtensions.UseMauiApp<TApp>(val);
		MauiApp val2 = val.Build();
		LinuxApplicationOptions linuxApplicationOptions = val2.Services.GetService<LinuxApplicationOptions>() ?? new LinuxApplicationOptions();
		ParseCommandLineOptions(args, linuxApplicationOptions);
		GtkHostService.Instance.Initialize(linuxApplicationOptions.Title, linuxApplicationOptions.Width, linuxApplicationOptions.Height);
		Console.WriteLine("[LinuxProgramHost] GTK initialized for WebView support");
		using LinuxApplication linuxApplication = new LinuxApplication();
		linuxApplication.Initialize(linuxApplicationOptions);
		LinuxMauiContext mauiContext = new LinuxMauiContext(val2.Services, linuxApplication);
		IApplication service = val2.Services.GetService<IApplication>();
		Application val3 = (Application)(object)((service is Application) ? service : null);
		if (val3 != null && Application.Current == null)
		{
			typeof(Application).GetProperty("Current")?.SetValue(null, val3);
		}
		SkiaView skiaView = null;
		if (service != null)
		{
			skiaView = RenderApplication(service, mauiContext, linuxApplicationOptions);
		}
		if (skiaView == null)
		{
			Console.WriteLine("No application page found. Showing demo UI.");
			skiaView = CreateDemoView();
		}
		linuxApplication.RootView = skiaView;
		linuxApplication.Run();
	}

	private static SkiaView? RenderApplication(IApplication application, LinuxMauiContext mauiContext, LinuxApplicationOptions options)
	{
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Expected O, but got Unknown
		try
		{
			Application val = (Application)(object)((application is Application) ? application : null);
			if (val != null)
			{
				Page val2 = val.MainPage;
				if (val2 == null && application.Windows.Count > 0)
				{
					IView content = application.Windows[0].Content;
					Page val3 = (Page)(object)((content is Page) ? content : null);
					if (val3 != null)
					{
						val2 = val3;
					}
				}
				if (val2 != null)
				{
					if (val.Windows.Count == 0)
					{
						Window val4 = new Window(val2);
						val.OpenWindow(val4);
						if (val.Windows.Count == 0 && typeof(Application).GetField("_windows", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(val) is IList list)
						{
							list.Add(val4);
						}
					}
					return RenderPage(val2, mauiContext);
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			Console.WriteLine("Error rendering application: " + ex.Message);
			Console.WriteLine(ex.StackTrace);
			return null;
		}
	}

	private static SkiaView? RenderPage(Page page, LinuxMauiContext mauiContext)
	{
		return new LinuxViewRenderer((IMauiContext)(object)mauiContext).RenderPage(page);
	}

	private static void ParseCommandLineOptions(string[] args, LinuxApplicationOptions options)
	{
		for (int i = 0; i < args.Length; i++)
		{
			switch (args[i].ToLowerInvariant())
			{
			case "--title":
				if (i + 1 < args.Length)
				{
					options.Title = args[++i];
				}
				break;
			case "--width":
			{
				if (i + 1 < args.Length && int.TryParse(args[i + 1], out var result2))
				{
					options.Width = result2;
					i++;
				}
				break;
			}
			case "--height":
			{
				if (i + 1 < args.Length && int.TryParse(args[i + 1], out var result))
				{
					options.Height = result;
					i++;
				}
				break;
			}
			case "--demo":
				options.ForceDemo = true;
				break;
			}
		}
	}

	public static SkiaView CreateDemoView()
	{
		//IL_0033: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0080: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_0112: Unknown result type (might be due to invalid IL or missing references)
		//IL_013e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0171: Unknown result type (might be due to invalid IL or missing references)
		//IL_01ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_022a: Unknown result type (might be due to invalid IL or missing references)
		//IL_023a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0297: Unknown result type (might be due to invalid IL or missing references)
		//IL_02a3: Unknown result type (might be due to invalid IL or missing references)
		//IL_02de: Unknown result type (might be due to invalid IL or missing references)
		//IL_02ea: Unknown result type (might be due to invalid IL or missing references)
		//IL_0393: Unknown result type (might be due to invalid IL or missing references)
		//IL_041f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0814: Unknown result type (might be due to invalid IL or missing references)
		//IL_088c: Unknown result type (might be due to invalid IL or missing references)
		//IL_093a: Unknown result type (might be due to invalid IL or missing references)
		//IL_09ee: Unknown result type (might be due to invalid IL or missing references)
		//IL_0a97: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b18: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b32: Unknown result type (might be due to invalid IL or missing references)
		//IL_0b6e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ba9: Unknown result type (might be due to invalid IL or missing references)
		//IL_0c86: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d0d: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d32: Unknown result type (might be due to invalid IL or missing references)
		//IL_0d78: Unknown result type (might be due to invalid IL or missing references)
		//IL_0daa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0e60: Unknown result type (might be due to invalid IL or missing references)
		//IL_0ea8: Unknown result type (might be due to invalid IL or missing references)
		//IL_0edb: Unknown result type (might be due to invalid IL or missing references)
		SkiaScrollView skiaScrollView = new SkiaScrollView();
		SkiaStackLayout skiaStackLayout = new SkiaStackLayout
		{
			Orientation = StackOrientation.Vertical,
			Spacing = 15f,
			BackgroundColor = new SKColor((byte)245, (byte)245, (byte)245)
		};
		skiaStackLayout.Padding = new SKRect(20f, 20f, 20f, 20f);
		skiaStackLayout.AddChild(new SkiaLabel
		{
			Text = "OpenMaui Linux Control Demo",
			FontSize = 28f,
			TextColor = new SKColor((byte)26, (byte)35, (byte)126),
			IsBold = true
		});
		skiaStackLayout.AddChild(new SkiaLabel
		{
			Text = "All controls rendered using SkiaSharp on X11",
			FontSize = 14f,
			TextColor = SKColors.Gray
		});
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Labels"));
		SkiaStackLayout skiaStackLayout2 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Vertical,
			Spacing = 5f
		};
		skiaStackLayout2.AddChild(new SkiaLabel
		{
			Text = "Normal Label",
			FontSize = 16f,
			TextColor = SKColors.Black
		});
		skiaStackLayout2.AddChild(new SkiaLabel
		{
			Text = "Bold Label",
			FontSize = 16f,
			TextColor = SKColors.Black,
			IsBold = true
		});
		skiaStackLayout2.AddChild(new SkiaLabel
		{
			Text = "Italic Label",
			FontSize = 16f,
			TextColor = SKColors.Gray,
			IsItalic = true
		});
		skiaStackLayout2.AddChild(new SkiaLabel
		{
			Text = "Colored Label (Pink)",
			FontSize = 16f,
			TextColor = new SKColor((byte)233, (byte)30, (byte)99)
		});
		skiaStackLayout.AddChild(skiaStackLayout2);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Buttons"));
		SkiaStackLayout skiaStackLayout3 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 10f
		};
		SkiaButton btnPrimary = new SkiaButton
		{
			Text = "Primary",
			FontSize = 14f
		};
		btnPrimary.BackgroundColor = new SKColor((byte)33, (byte)150, (byte)243);
		btnPrimary.TextColor = SKColors.White;
		int clickCount = 0;
		btnPrimary.Clicked += delegate
		{
			clickCount++;
			btnPrimary.Text = $"Clicked {clickCount}x";
		};
		skiaStackLayout3.AddChild(btnPrimary);
		SkiaButton skiaButton = new SkiaButton
		{
			Text = "Success",
			FontSize = 14f
		};
		skiaButton.BackgroundColor = new SKColor((byte)76, (byte)175, (byte)80);
		skiaButton.TextColor = SKColors.White;
		skiaStackLayout3.AddChild(skiaButton);
		SkiaButton skiaButton2 = new SkiaButton
		{
			Text = "Danger",
			FontSize = 14f
		};
		skiaButton2.BackgroundColor = new SKColor((byte)244, (byte)67, (byte)54);
		skiaButton2.TextColor = SKColors.White;
		skiaStackLayout3.AddChild(skiaButton2);
		skiaStackLayout.AddChild(skiaStackLayout3);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Text Entry"));
		SkiaEntry child = new SkiaEntry
		{
			Placeholder = "Type here...",
			FontSize = 14f
		};
		skiaStackLayout.AddChild(child);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("SearchBar"));
		SkiaSearchBar searchBar = new SkiaSearchBar
		{
			Placeholder = "Search for items..."
		};
		SkiaLabel searchResultLabel = new SkiaLabel
		{
			Text = "",
			FontSize = 12f,
			TextColor = SKColors.Gray
		};
		searchBar.TextChanged += delegate(object? s, TextChangedEventArgs e)
		{
			searchResultLabel.Text = "Searching: " + e.NewTextValue;
		};
		searchBar.SearchButtonPressed += delegate
		{
			searchResultLabel.Text = "Search submitted: " + searchBar.Text;
		};
		skiaStackLayout.AddChild(searchBar);
		skiaStackLayout.AddChild(searchResultLabel);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Editor (Multi-line)"));
		SkiaEditor child2 = new SkiaEditor
		{
			Placeholder = "Enter multiple lines of text...",
			FontSize = 14f,
			BackgroundColor = SKColors.White
		};
		skiaStackLayout.AddChild(child2);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("CheckBox"));
		SkiaStackLayout skiaStackLayout4 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 20f
		};
		SkiaCheckBox child3 = new SkiaCheckBox
		{
			IsChecked = true
		};
		skiaStackLayout4.AddChild(child3);
		skiaStackLayout4.AddChild(new SkiaLabel
		{
			Text = "Checked",
			FontSize = 14f
		});
		SkiaCheckBox child4 = new SkiaCheckBox
		{
			IsChecked = false
		};
		skiaStackLayout4.AddChild(child4);
		skiaStackLayout4.AddChild(new SkiaLabel
		{
			Text = "Unchecked",
			FontSize = 14f
		});
		skiaStackLayout.AddChild(skiaStackLayout4);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Switch"));
		SkiaStackLayout skiaStackLayout5 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 20f
		};
		SkiaSwitch child5 = new SkiaSwitch
		{
			IsOn = true
		};
		skiaStackLayout5.AddChild(child5);
		skiaStackLayout5.AddChild(new SkiaLabel
		{
			Text = "On",
			FontSize = 14f
		});
		SkiaSwitch child6 = new SkiaSwitch
		{
			IsOn = false
		};
		skiaStackLayout5.AddChild(child6);
		skiaStackLayout5.AddChild(new SkiaLabel
		{
			Text = "Off",
			FontSize = 14f
		});
		skiaStackLayout.AddChild(skiaStackLayout5);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("RadioButton"));
		SkiaStackLayout skiaStackLayout6 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 15f
		};
		skiaStackLayout6.AddChild(new SkiaRadioButton
		{
			Content = "Option A",
			IsChecked = true,
			GroupName = "demo"
		});
		skiaStackLayout6.AddChild(new SkiaRadioButton
		{
			Content = "Option B",
			IsChecked = false,
			GroupName = "demo"
		});
		skiaStackLayout6.AddChild(new SkiaRadioButton
		{
			Content = "Option C",
			IsChecked = false,
			GroupName = "demo"
		});
		skiaStackLayout.AddChild(skiaStackLayout6);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Slider"));
		SkiaLabel sliderLabel = new SkiaLabel
		{
			Text = "Value: 50",
			FontSize = 14f
		};
		SkiaSlider slider = new SkiaSlider
		{
			Minimum = 0.0,
			Maximum = 100.0,
			Value = 50.0
		};
		slider.ValueChanged += delegate
		{
			sliderLabel.Text = $"Value: {(int)slider.Value}";
		};
		skiaStackLayout.AddChild(slider);
		skiaStackLayout.AddChild(sliderLabel);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Stepper"));
		SkiaStackLayout skiaStackLayout7 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 10f
		};
		SkiaLabel stepperLabel = new SkiaLabel
		{
			Text = "Value: 5",
			FontSize = 14f
		};
		SkiaStepper stepper = new SkiaStepper
		{
			Value = 5.0,
			Minimum = 0.0,
			Maximum = 10.0,
			Increment = 1.0
		};
		stepper.ValueChanged += delegate
		{
			stepperLabel.Text = $"Value: {(int)stepper.Value}";
		};
		skiaStackLayout7.AddChild(stepper);
		skiaStackLayout7.AddChild(stepperLabel);
		skiaStackLayout.AddChild(skiaStackLayout7);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("ProgressBar"));
		SkiaProgressBar child7 = new SkiaProgressBar
		{
			Progress = 0.699999988079071
		};
		skiaStackLayout.AddChild(child7);
		skiaStackLayout.AddChild(new SkiaLabel
		{
			Text = "70% Complete",
			FontSize = 12f,
			TextColor = SKColors.Gray
		});
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("ActivityIndicator"));
		SkiaStackLayout skiaStackLayout8 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 10f
		};
		SkiaActivityIndicator child8 = new SkiaActivityIndicator
		{
			IsRunning = true
		};
		skiaStackLayout8.AddChild(child8);
		skiaStackLayout8.AddChild(new SkiaLabel
		{
			Text = "Loading...",
			FontSize = 14f,
			TextColor = SKColors.Gray
		});
		skiaStackLayout.AddChild(skiaStackLayout8);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Picker (Dropdown)"));
		SkiaPicker picker = new SkiaPicker
		{
			Title = "Select an item"
		};
		picker.SetItems(new string[7] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape" });
		SkiaLabel pickerLabel = new SkiaLabel
		{
			Text = "Selected: (none)",
			FontSize = 12f,
			TextColor = SKColors.Gray
		};
		picker.SelectedIndexChanged += delegate
		{
			pickerLabel.Text = "Selected: " + picker.SelectedItem;
		};
		skiaStackLayout.AddChild(picker);
		skiaStackLayout.AddChild(pickerLabel);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("DatePicker"));
		SkiaDatePicker datePicker = new SkiaDatePicker
		{
			Date = DateTime.Today
		};
		SkiaLabel dateLabel = new SkiaLabel
		{
			Text = $"Date: {DateTime.Today:d}",
			FontSize = 12f,
			TextColor = SKColors.Gray
		};
		datePicker.DateSelected += delegate
		{
			dateLabel.Text = $"Date: {datePicker.Date:d}";
		};
		skiaStackLayout.AddChild(datePicker);
		skiaStackLayout.AddChild(dateLabel);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("TimePicker"));
		SkiaTimePicker timePicker = new SkiaTimePicker();
		SkiaLabel timeLabel = new SkiaLabel
		{
			Text = $"Time: {DateTime.Now:t}",
			FontSize = 12f,
			TextColor = SKColors.Gray
		};
		timePicker.TimeSelected += delegate
		{
			timeLabel.Text = $"Time: {DateTime.Today.Add(timePicker.Time):t}";
		};
		skiaStackLayout.AddChild(timePicker);
		skiaStackLayout.AddChild(timeLabel);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Border"));
		SkiaBorder skiaBorder = new SkiaBorder
		{
			CornerRadius = 8f,
			StrokeThickness = 2f,
			Stroke = new SKColor((byte)33, (byte)150, (byte)243),
			BackgroundColor = new SKColor((byte)227, (byte)242, (byte)253)
		};
		skiaBorder.SetPadding(15f);
		skiaBorder.AddChild(new SkiaLabel
		{
			Text = "Content inside a styled Border",
			FontSize = 14f,
			TextColor = new SKColor((byte)26, (byte)35, (byte)126)
		});
		skiaStackLayout.AddChild(skiaBorder);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Frame (with shadow)"));
		SkiaFrame skiaFrame = new SkiaFrame();
		skiaFrame.BackgroundColor = SKColors.White;
		skiaFrame.AddChild(new SkiaLabel
		{
			Text = "Content inside a Frame with shadow effect",
			FontSize = 14f
		});
		skiaStackLayout.AddChild(skiaFrame);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("CollectionView (List)"));
		SkiaCollectionView skiaCollectionView = new SkiaCollectionView
		{
			SelectionMode = SkiaSelectionMode.Single,
			Header = "Fruits",
			Footer = "End of list"
		};
		skiaCollectionView.ItemsSource = new object[8] { "Apple", "Banana", "Cherry", "Date", "Elderberry", "Fig", "Grape", "Honeydew" };
		SkiaLabel collectionLabel = new SkiaLabel
		{
			Text = "Selected: (none)",
			FontSize = 12f,
			TextColor = SKColors.Gray
		};
		skiaCollectionView.SelectionChanged += delegate(object? s, CollectionSelectionChangedEventArgs e)
		{
			object value = e.CurrentSelection.FirstOrDefault();
			collectionLabel.Text = $"Selected: {value}";
		};
		skiaStackLayout.AddChild(skiaCollectionView);
		skiaStackLayout.AddChild(collectionLabel);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("ImageButton"));
		SkiaStackLayout skiaStackLayout9 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 10f
		};
		SkiaImageButton skiaImageButton = new SkiaImageButton
		{
			CornerRadius = 8f,
			StrokeColor = new SKColor((byte)33, (byte)150, (byte)243),
			StrokeThickness = 1f,
			BackgroundColor = new SKColor((byte)227, (byte)242, (byte)253),
			PaddingLeft = 10f,
			PaddingRight = 10f,
			PaddingTop = 10f,
			PaddingBottom = 10f
		};
		SKBitmap bitmap = CreateStarIcon(32, new SKColor((byte)33, (byte)150, (byte)243));
		skiaImageButton.Bitmap = bitmap;
		SkiaLabel imgBtnLabel = new SkiaLabel
		{
			Text = "Click the star!",
			FontSize = 12f,
			TextColor = SKColors.Gray
		};
		skiaImageButton.Clicked += delegate
		{
			imgBtnLabel.Text = "Star clicked!";
		};
		skiaStackLayout9.AddChild(skiaImageButton);
		skiaStackLayout9.AddChild(imgBtnLabel);
		skiaStackLayout.AddChild(skiaStackLayout9);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(CreateSectionHeader("Image"));
		SkiaStackLayout skiaStackLayout10 = new SkiaStackLayout
		{
			Orientation = StackOrientation.Horizontal,
			Spacing = 10f
		};
		SkiaImage skiaImage = new SkiaImage();
		SKBitmap bitmap2 = CreateSampleImage(80, 60);
		skiaImage.Bitmap = bitmap2;
		skiaStackLayout10.AddChild(skiaImage);
		skiaStackLayout10.AddChild(new SkiaLabel
		{
			Text = "Sample generated image",
			FontSize = 12f,
			TextColor = SKColors.Gray
		});
		skiaStackLayout.AddChild(skiaStackLayout10);
		skiaStackLayout.AddChild(CreateSeparator());
		skiaStackLayout.AddChild(new SkiaLabel
		{
			Text = "All 25+ controls are interactive - try them all!",
			FontSize = 16f,
			TextColor = new SKColor((byte)76, (byte)175, (byte)80),
			IsBold = true
		});
		skiaStackLayout.AddChild(new SkiaLabel
		{
			Text = "Scroll down to see more controls",
			FontSize = 12f,
			TextColor = SKColors.Gray
		});
		skiaScrollView.Content = skiaStackLayout;
		return skiaScrollView;
	}

	private static SkiaLabel CreateSectionHeader(string text)
	{
		//IL_001e: Unknown result type (might be due to invalid IL or missing references)
		return new SkiaLabel
		{
			Text = text,
			FontSize = 18f,
			TextColor = new SKColor((byte)55, (byte)71, (byte)79),
			IsBold = true
		};
	}

	private static SkiaView CreateSeparator()
	{
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		return new SkiaLabel
		{
			Text = "",
			BackgroundColor = new SKColor((byte)224, (byte)224, (byte)224),
			RequestedHeight = 1.0
		};
	}

	private static SKBitmap CreateStarIcon(int size, SKColor color)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected O, but got Unknown
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_0011: Unknown result type (might be due to invalid IL or missing references)
		//IL_001b: Unknown result type (might be due to invalid IL or missing references)
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0021: Unknown result type (might be due to invalid IL or missing references)
		//IL_0027: Unknown result type (might be due to invalid IL or missing references)
		//IL_002e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0036: Expected O, but got Unknown
		//IL_0036: Unknown result type (might be due to invalid IL or missing references)
		//IL_003c: Expected O, but got Unknown
		SKBitmap val = new SKBitmap(size, size, false);
		SKCanvas val2 = new SKCanvas(val);
		try
		{
			val2.Clear(SKColors.Transparent);
			SKPaint val3 = new SKPaint
			{
				Color = color,
				Style = (SKPaintStyle)0,
				IsAntialias = true
			};
			try
			{
				SKPath val4 = new SKPath();
				try
				{
					float num = (float)size / 2f;
					float num2 = (float)size / 2f;
					float num3 = (float)size / 2f - 2f;
					float num4 = num3 * 0.4f;
					for (int i = 0; i < 5; i++)
					{
						double num5 = (double)(i * 72 - 90) * Math.PI / 180.0;
						double num6 = (double)(i * 72 + 36 - 90) * Math.PI / 180.0;
						float num7 = num + num3 * (float)Math.Cos(num5);
						float num8 = num2 + num3 * (float)Math.Sin(num5);
						float num9 = num + num4 * (float)Math.Cos(num6);
						float num10 = num2 + num4 * (float)Math.Sin(num6);
						if (i == 0)
						{
							val4.MoveTo(num7, num8);
						}
						else
						{
							val4.LineTo(num7, num8);
						}
						val4.LineTo(num9, num10);
					}
					val4.Close();
					val2.DrawPath(val4, val3);
					return val;
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}

	private static SKBitmap CreateSampleImage(int width, int height)
	{
		//IL_0003: Unknown result type (might be due to invalid IL or missing references)
		//IL_0009: Expected O, but got Unknown
		//IL_000a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0010: Expected O, but got Unknown
		//IL_0010: Unknown result type (might be due to invalid IL or missing references)
		//IL_0016: Expected O, but got Unknown
		//IL_0020: Unknown result type (might be due to invalid IL or missing references)
		//IL_0029: Unknown result type (might be due to invalid IL or missing references)
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0047: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0092: Unknown result type (might be due to invalid IL or missing references)
		//IL_0097: Unknown result type (might be due to invalid IL or missing references)
		//IL_00a2: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ac: Unknown result type (might be due to invalid IL or missing references)
		//IL_00b3: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bc: Expected O, but got Unknown
		//IL_010f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0116: Expected O, but got Unknown
		//IL_0118: Unknown result type (might be due to invalid IL or missing references)
		//IL_011d: Unknown result type (might be due to invalid IL or missing references)
		//IL_011e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0128: Unknown result type (might be due to invalid IL or missing references)
		//IL_0131: Expected O, but got Unknown
		SKBitmap val = new SKBitmap(width, height, false);
		SKCanvas val2 = new SKCanvas(val);
		try
		{
			SKPaint val3 = new SKPaint();
			try
			{
				SKShader val4 = SKShader.CreateLinearGradient(new SKPoint(0f, 0f), new SKPoint((float)width, (float)height), (SKColor[])(object)new SKColor[2]
				{
					new SKColor((byte)66, (byte)165, (byte)245),
					new SKColor((byte)126, (byte)87, (byte)194)
				}, new float[2] { 0f, 1f }, (SKShaderTileMode)0);
				try
				{
					val3.Shader = val4;
					val2.DrawRect(0f, 0f, (float)width, (float)height, val3);
					SKPaint val5 = new SKPaint
					{
						Color = ((SKColor)(ref SKColors.White)).WithAlpha((byte)180),
						Style = (SKPaintStyle)0,
						IsAntialias = true
					};
					try
					{
						val2.DrawCircle((float)width * 0.3f, (float)height * 0.4f, 15f, val5);
						val2.DrawRect((float)width * 0.5f, (float)height * 0.3f, 20f, 20f, val5);
						SKFont val6 = new SKFont(SKTypeface.Default, 12f, 1f, 0f);
						try
						{
							SKPaint val7 = new SKPaint(val6)
							{
								Color = SKColors.White,
								IsAntialias = true
							};
							try
							{
								val2.DrawText("IMG", 10f, (float)(height - 8), val7);
								return val;
							}
							finally
							{
								((IDisposable)val7)?.Dispose();
							}
						}
						finally
						{
							((IDisposable)val6)?.Dispose();
						}
					}
					finally
					{
						((IDisposable)val5)?.Dispose();
					}
				}
				finally
				{
					((IDisposable)val4)?.Dispose();
				}
			}
			finally
			{
				((IDisposable)val3)?.Dispose();
			}
		}
		finally
		{
			((IDisposable)val2)?.Dispose();
		}
	}
}
