using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media;
using System.Xml;
using TaskDialogInterop;
using Unclassified.FieldLog;
using Unclassified.TxEditor.Models;
using Unclassified.TxEditor.UI;
using Unclassified.TxEditor.Views;
using Unclassified.TxLib;
using Unclassified.UI;
using Unclassified.Util;
using Clipboard = System.Windows.Clipboard;
using IDataObject = System.Windows.IDataObject;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using SaveFileDialog = Microsoft.Win32.SaveFileDialog;

namespace Unclassified.TxEditor.ViewModels
{
	internal class MainViewModel : ViewModelBase, IViewCommandSource
	{
		#region Static data

		public static MainViewModel Instance { get; private set; }

        public static SerializedTranslation DumpTranslation(TextKeyViewModel model)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            var mainModel = model.MainWindowVM;
            var modelsWithKeys = model.FindViewModels(args =>
            {
                var textModel = args.Item as TextKeyViewModel;
                args.IncludeInResult = textModel?.IsFullKey == true && !string.IsNullOrEmpty(textModel.TextKey);
            }).Cast<TextKeyViewModel>().ToArray();

            var cultures = mainModel.LoadedCultureNames.Union(mainModel.DeletedCultureNames)
                                    .Distinct()
                                    .ToDictionary(name => name,
                                                  name => new SerializedCulture
                                                  {
                                                      Name = name,
                                                      IsPrimary = Equals(name, mainModel.PrimaryCulture)
                                                  });

            var primaryCulture = mainModel.PrimaryCulture != null ? cultures[mainModel.PrimaryCulture] : cultures.Values.FirstOrDefault();

            foreach (var textKeyViewModel in modelsWithKeys)
            {
                var textKey = textKeyViewModel.TextKey;

                foreach (var cultureTextViewModel in textKeyViewModel.CultureTextVMs)
                {
                    //Add base key
                    var culture = cultures[cultureTextViewModel.CultureName];
                    Func<string, bool> addKeyPredicate = value =>
                    {
                        
                        if (string.IsNullOrEmpty(cultureTextViewModel.Text))
                        {
                            if (!textKey.StartsWith("Tx:") && Equals(culture, primaryCulture)) return true;
                            if (cultureTextViewModel.AcceptMissing) return true;
                            if (cultureTextViewModel.AcceptPlaceholders) return true;
                            if (cultureTextViewModel.AcceptPunctuation) return true;
                            return false;
                        }
                        return true;
                    };

                    if (addKeyPredicate(cultureTextViewModel.Text))
                    {
                        culture.Keys.Add(new SerializedKey
                        {
                            Key = textKey,
                            Text = cultureTextViewModel.Text,
                            Comment = textKeyViewModel.Comment,
                            AcceptMissing = cultureTextViewModel.AcceptMissing,
                            AcceptPlaceholders = cultureTextViewModel.AcceptPlaceholders,
                            AcceptPunctuation = cultureTextViewModel.AcceptPunctuation
                        });
                    }

                    foreach (var quantifiedTextViewModel in cultureTextViewModel.QuantifiedTextVMs)
                    {
                        if (!addKeyPredicate(quantifiedTextViewModel.Text)) continue;

                        culture.Keys.Add(new SerializedKey
                        {
                            Key = textKey,
                            Text = quantifiedTextViewModel.Text,
                            AcceptMissing = quantifiedTextViewModel.AcceptMissing,
                            AcceptPlaceholders = quantifiedTextViewModel.AcceptPlaceholders,
                            AcceptPunctuation = quantifiedTextViewModel.AcceptPunctuation,
                            Count = quantifiedTextViewModel.Count,
                            Modulo = quantifiedTextViewModel.Modulo
                        });
                    }
                }
            }
            var rootModel = model.FindRoot();
            return new SerializedTranslation
            {
                IsTemplate = rootModel?.IsTemplate == true,
                Name = rootModel.DisplayName,
                Cultures = cultures.Values.ToList()
            };
        }

        #endregion Static data

        #region Private data

		private List<TextKeyViewModel> selectedTextKeys;
		private List<TextKeyViewModel> viewHistory = new List<TextKeyViewModel>();
		private int viewHistoryIndex;
		private OpFlag navigatingHistory = new OpFlag();
		private DateTimeWindow dateTimeWindow;

		#endregion Private data

		#region Constructor

		public MainViewModel()
		{
			Instance = this;

			TextKeys = new Dictionary<string, List<TextKeyViewModel>>();
			LoadedCultureNames = new HashSet<string>();
			DeletedCultureNames = new HashSet<string>();
            RootKeys = new ObservableCollection<RootKeyViewModel>();
			ProblemKeys = new ObservableHashSet<TextKeyViewModel>();

			searchDc = DelayedCall.Create(UpdateSearch, 250);
			SearchText = "";   // Change value once to set the clear button visibility
			ClearViewHistory();
			UpdateTitle();

			FontScale = App.Settings.View.FontScale;

			App.Settings.View.OnPropertyChanged(s => s.ShowSuggestions, UpdateSuggestionsLayout);
			App.Settings.View.OnPropertyChanged(s => s.SuggestionsHorizontalLayout, UpdateSuggestionsLayout);
			UpdateSuggestionsLayout();
		}

		#endregion Constructor

		#region Public properties

		/// <summary>
		/// Dictionary of all loaded text keys, associating a text key string with its TextKeyViewModel instance.
		/// </summary>
		public Dictionary<string, List<TextKeyViewModel>> TextKeys { get; private set; }
		public HashSet<string> LoadedCultureNames { get; private set; }
		public HashSet<string> DeletedCultureNames { get; private set; }
		//public RootKeyViewModel RootTextKey { get; private set; }

        public ObservableCollection<RootKeyViewModel> RootKeys { get; private set; }
        
        public ObservableHashSet<TextKeyViewModel> ProblemKeys { get; private set; }

		public string ScanDirectory { get; set; }

		#endregion Public properties

		#region Data properties

		public IAppSettings Settings
		{
			get { return App.Settings; }
		}

		public string PrimaryCulture
		{
			get { return GetValue<string>("PrimaryCulture"); }
			set { SetValue(value, "PrimaryCulture"); }
		}

		public bool ProblemFilterActive
		{
			get
			{
				return GetValue<bool>("ProblemFilterActive");
			}
			set
			{
				if (SetValue(BooleanBoxes.Box(value), "ProblemFilterActive"))
				{
					UpdateSearch();
				}
			}
		}

		public string CursorChar
		{
			get { return GetValue<string>("CursorChar"); }
			set { SetValue(value, "CursorChar"); }
		}

		[NotifiesOn("CursorChar")]
		public string CursorCharCodePoint
		{
			get { return CursorChar != null ? "U+" + ((int)CursorChar[0]).ToString("X4") : ""; }
		}

		[NotifiesOn("CursorChar")]
		public string CursorCharName
		{
			get { return CursorChar != null ? UnicodeInfo.GetChar(CursorChar[0]).Name : ""; }
		}

		[NotifiesOn("CursorChar")]
		public string CursorCharCategory
		{
			get { return CursorChar != null ? UnicodeInfo.GetChar(CursorChar[0]).Category : Tx.T("statusbar.char info.no character at cursor"); }
		}

		[NotifiesOn("CursorChar")]
		public Visibility CursorCharVisibility
		{
			get { return CursorChar != null ? Visibility.Visible : Visibility.Collapsed; }
		}

		// TODO: Move entirely to AppSettings
		private double fontScale = 100;
		public double FontScale
		{
			get
			{
				return fontScale;
			}
			set
			{
				if (CheckUpdate(value, ref fontScale, "FontScale", "FontSize", "TextFormattingMode"))
				{
					App.Settings.View.FontScale = fontScale;
				}
			}
		}

		// TODO: Move to separate converter
		public double FontSize
		{
			get { return fontScale / 100 * 12; }
		}

		// TODO: Move to separate converter (use FontSize as input, make it universally usable)
		public TextFormattingMode TextFormattingMode
		{
			get { return FontSize < 16 ? TextFormattingMode.Display : TextFormattingMode.Ideal; }
		}

		public string StatusText
		{
			get
			{
				return GetValue<string>("StatusText");
			}
			set
			{
				if (SetValue(value, "StatusText"))
				{
					ViewCommandManager.Invoke("AnimateStatusText", StatusText);
				}
			}
		}

		public string SelectedCulture
		{
			get
			{
				return GetValue<string>("SelectedCulture");
			}
			set
			{
				if (SetValue(value, "SelectedCulture"))
				{
					DeleteCultureCommand.RaiseCanExecuteChanged();
					SetPrimaryCultureCommand.RaiseCanExecuteChanged();
					if (SelectedCulture != null)
					{
						LastSelectedCulture = SelectedCulture;
					}
				}
			}
		}

		public string LastSelectedCulture
		{
			get
			{
				return GetValue<string>("LastSelectedCulture");
			}
			set
			{
				if (SetValue(value, "LastSelectedCulture"))
				{
					UpdateSuggestionsLater();
				}
			}
		}

		public bool HaveComment
		{
			get { return GetValue<bool>("HaveComment"); }
			set { SetValue(BooleanBoxes.Box(value), "HaveComment"); }
		}

		public double SuggestionsPanelWidth
		{
			get
			{
				return GetValue<double>("SuggestionsPanelWidth");
			}
			set
			{
				if (SetValue(value, "SuggestionsPanelWidth"))
				{
					if (App.Settings.View.ShowSuggestions && App.Settings.View.SuggestionsHorizontalLayout)
					{
						App.Settings.View.SuggestionsWidth = SuggestionsPanelWidth;
					}
				}
			}
		}

		public double SuggestionsPanelHeight
		{
			get
			{
				return GetValue<double>("SuggestionsPanelHeight");
			}
			set
			{
				if (SetValue(value, "SuggestionsPanelHeight"))
				{
					if (App.Settings.View.ShowSuggestions && !App.Settings.View.SuggestionsHorizontalLayout)
					{
						App.Settings.View.SuggestionsHeight = SuggestionsPanelHeight;
					}
				}
			}
		}

		public double SuggestionsSplitterWidth
		{
			get { return GetValue<double>("SuggestionsSplitterWidth"); }
			set { SetValue(value, "SuggestionsSplitterWidth"); }
		}

		public double SuggestionsSplitterHeight
		{
			get { return GetValue<double>("SuggestionsSplitterHeight"); }
			set { SetValue(value, "SuggestionsSplitterHeight"); }
		}

		private ObservableCollection<SuggestionViewModel> suggestions = new ObservableCollection<SuggestionViewModel>();
		public ObservableCollection<SuggestionViewModel> Suggestions
		{
			get { return suggestions; }
		}

		public bool HaveSuggestions
		{
			get { return GetValue<bool>("HaveSuggestions"); }
			set { SetValue(BooleanBoxes.Box(value), "HaveSuggestions"); }
		}

		public string SuggestionsCulture
		{
			get { return GetValue<string>("SuggestionsCulture"); }
			set { SetValue(value, "SuggestionsCulture"); }
		}

		[NotifiesOn("SuggestionsCulture")]
		public string SuggestionsCultureCaption
		{
			get
			{
				if (!string.IsNullOrEmpty(SuggestionsCulture))
					return Tx.TC("suggestions.caption for", "name", SuggestionsCulture);
				else
					return Tx.TC("suggestions.caption");
			}
		}

		public int SelectionDummy
		{
			get { return 0; }
		}

		#endregion Data properties

		#region Command definition and initialisation

		// Toolbar commands
		// File section
		public DelegateCommand NewTranslationCommand { get; private set; }
		public DelegateCommand LoadFolderCommand { get; private set; }
		public DelegateCommand LoadFileCommand { get; private set; }
		public DelegateCommand SaveCommand { get; private set; }
		public DelegateCommand ImportFileCommand { get; private set; }
		public DelegateCommand ExportKeysCommand { get; private set; }
		// Culture section
		public DelegateCommand NewCultureCommand { get; private set; }
		public DelegateCommand DeleteCultureCommand { get; private set; }
		public DelegateCommand ReplaceCultureCommand { get; private set; }
		public DelegateCommand InsertSystemKeysCommand { get; private set; }
		public DelegateCommand ViewDateTimeFormatsCommand { get; private set; }
		public DelegateCommand SetPrimaryCultureCommand { get; private set; }
		// Text key section
		public DelegateCommand NewTextKeyCommand { get; private set; }
		public DelegateCommand DeleteTextKeyCommand { get; private set; }
		public DelegateCommand TextKeyWizardCommand { get; private set; }
		public DelegateCommand RenameTextKeyCommand { get; private set; }
		public DelegateCommand DuplicateTextKeyCommand { get; private set; }
		// View section
		public DelegateCommand NavigateBackCommand { get; private set; }
		public DelegateCommand NavigateForwardCommand { get; private set; }
		public DelegateCommand GotoDefinitionCommand { get; private set; }
		// Filter section
		public DelegateCommand ClearSearchCommand { get; private set; }
		// Application section
		public DelegateCommand SettingsCommand { get; private set; }
		public DelegateCommand AboutCommand { get; private set; }
		public DelegateCommand HelpCommand { get; private set; }
		public DelegateCommand LibFolderCommand { get; private set; }

		// Context menu
		public DelegateCommand ConvertToNamespaceCommand { get; private set; }
		public DelegateCommand ConvertToTextKeyCommand { get; private set; }

		// Other commands
		public DelegateCommand CopyTextKeyCommand { get; private set; }
		public DelegateCommand SelectPreviousTextKeyCommand { get; private set; }
		public DelegateCommand SelectNextTextKeyCommand { get; private set; }

		protected override void InitializeCommands()
		{
			// Toolbar
			// File section
			NewTranslationCommand = new DelegateCommand(OnNewTranslation);
			LoadFolderCommand = new DelegateCommand(OnLoadFolder);
			LoadFileCommand = new DelegateCommand(OnLoadFile);
		    SaveCommand = new DelegateCommand(OnSave, () => RootKeys.Any(k => k.HasUnsavedChanges));
			ImportFileCommand = new DelegateCommand(OnImportFile);
			ExportKeysCommand = new DelegateCommand(OnExportKeys, CanExportKeys);
			// Culture section
			NewCultureCommand = new DelegateCommand(OnNewCulture);
			DeleteCultureCommand = new DelegateCommand(OnDeleteCulture, CanDeleteCulture);
			ReplaceCultureCommand = new DelegateCommand(OnReplaceCulture);
			InsertSystemKeysCommand = new DelegateCommand(OnInsertSystemKeys);
			ViewDateTimeFormatsCommand = new DelegateCommand(OnViewDateTimeFormats);
			SetPrimaryCultureCommand = new DelegateCommand(OnSetPrimaryCulture, CanSetPrimaryCulture);
			// Text key section
			NewTextKeyCommand = new DelegateCommand(OnNewTextKey);
			DeleteTextKeyCommand = new DelegateCommand(OnDeleteTextKey, CanDeleteTextKey);
			TextKeyWizardCommand = new DelegateCommand(OnTextKeyWizard);
			RenameTextKeyCommand = new DelegateCommand(OnRenameTextKey, CanRenameTextKey);
			DuplicateTextKeyCommand = new DelegateCommand(OnDuplicateTextKey, CanDuplicateTextKey);
			// View section
			NavigateBackCommand = new DelegateCommand(OnNavigateBack, CanNavigateBack);
			NavigateForwardCommand = new DelegateCommand(OnNavigateForward, CanNavigateForward);
			GotoDefinitionCommand = new DelegateCommand(OnGotoDefinition, CanGotoDefinition);
			// Filter section
			ClearSearchCommand = new DelegateCommand(() => { SearchText = ""; });
			// Application section
			SettingsCommand = new DelegateCommand(OnSettings);
			AboutCommand = new DelegateCommand(OnAbout);
			HelpCommand = new DelegateCommand(OnHelp);
			LibFolderCommand = new DelegateCommand(OnLibFolder);

			// Context menu
			ConvertToNamespaceCommand = new DelegateCommand(OnConvertToNamespace, CanConvertToNamespace);
			ConvertToTextKeyCommand = new DelegateCommand(OnConvertToTextKey, CanConvertToTextKey);

			// Other commands
			CopyTextKeyCommand = new DelegateCommand(OnCopyTextKey, CanCopyTextKey);
			SelectPreviousTextKeyCommand = new DelegateCommand(OnSelectPreviousTextKey);
			SelectNextTextKeyCommand = new DelegateCommand(OnSelectNextTextKey);
		}

		#endregion Command definition and initialisation

		#region Toolbar command handlers

		#region File section

		internal bool CheckModifiedSaved()
		{
			if (RootKeys.Any(k=>k.HasUnsavedChanges))
			{
				var result = TaskDialog.Show(
					owner: MainWindow.Instance,
					title: "TxEditor",
					mainInstruction: Tx.T("msg.save.save changes"),
					content: Tx.T("msg.save.save changes.desc"),
					customButtons: new[] { Tx.T("task dialog.button.save"), Tx.T("task dialog.button.dont save"), Tx.T("task dialog.button.cancel") },
					allowDialogCancellation: true);

				if (result.CustomButtonResult == 0)
				{
					// Save
					return Save();
				}
			    if (result.CustomButtonResult != 1)
			    {
			        // Cancel or unset
			        return false;
			    }
			}
			return true;
		}

		private void OnNewTranslation()
		{
			if (!CheckModifiedSaved()) return;

			if (!dateTimeWindow.IsClosed()) dateTimeWindow.Close();
            RootKeys.Add(new RootKeyViewModel(this));

            StatusText = Tx.T("statusbar.new dictionary created");
			UpdateTitle();
		}

		private void OnLoadFolder()
		{
			if (!CheckModifiedSaved()) return;

		    var folderDlg = new OpenFolderDialog
		    {
		        Title = Tx.T("msg.load folder.title")
		    };
		    if (folderDlg.ShowDialog(new Wpf32Window(MainWindow.Instance)) == true)
			{
				DoLoadFolder(folderDlg.Folder);
			}
		}

	    public void DoLoadFolder(string folder)
	    {
	        var locations = new List<ISerializeLocation>();
	        foreach (var file in Util.PathHelper.EnumerateFiles(folder.TrimEnd('\\') + "\\"))
	        {
	            var localFile = file.ToLowerInvariant();
	            var extension = Path.GetExtension(localFile).ToLowerInvariant();
	            if (string.IsNullOrEmpty(extension)) continue;
	            if (extension.EndsWith(".xml") || extension.EndsWith(".txd")) locations.Add(new FileLocation(localFile));
	        }

	        Load(locations.ToArray());
	    }

	    private void OnLoadFile()
	    {
	        if (!CheckModifiedSaved()) return;

	        var fileDlg = new OpenFileDialog
	        {
	            CheckFileExists = true,
	            Filter = Tx.T("file filter.tx dictionary files") + " (*.txd)|*.txd|" +
	                     Tx.T("file filter.xml files") + " (*.xml)|*.xml|" +
	                     Tx.T("file filter.all files") + " (*.*)|*.*",
	            Multiselect = true,
	            ShowReadOnly = false,
	            Title = Tx.T("msg.load file.title")
	        };
	        if (fileDlg.ShowDialog(MainWindow.Instance) == true)
	        {
	            Load(fileDlg.FileNames.Select(f => new FileLocation(f)).Cast<ISerializeLocation>().ToArray());
	        }
	    }

	    public void Load(params ISerializeLocation[] locations)
	    {
	        var translations = SerializeProvider.Instance.DetectUniqueTranslations(locations).ToArray();

	        int filesLoaded = 0;
	        foreach (var detectedTranslation in translations)
	        {
	            var loadMissedRelatedLocationsResult = DialogHelper.LoadMissedRelatedLocations(detectedTranslation);
	            if (loadMissedRelatedLocationsResult == DialogResult.Cancel) continue;

	            var instructions = detectedTranslation.DeserializeInstructions;
	            if (loadMissedRelatedLocationsResult == DialogResult.OK)
	                instructions = instructions.Union(detectedTranslation.RelatedMissedInstructions).ToArray();

	            var root = RootKeys.FirstOrDefault(r => Equals(r.Location, detectedTranslation.Description.Location));
	            root = root ?? new RootKeyViewModel(this)
	            {
	                Location = detectedTranslation.Description.Location,
	                Serializer = detectedTranslation.Description.Serializer,
	                DisplayName = detectedTranslation.Description.Name
	            };

	            var importResults = instructions.Select(i => Import(root, i)).ToArray();
	            if (!importResults.Any(r => r == DialogResult.Cancel))
	            {
                    filesLoaded += importResults.Count(r => r == DialogResult.OK);
                    if(!RootKeys.Contains(root)) RootKeys.Add(root);
                    SortCulturesInTextKey(root);
                }
	        }
            
            DeletedCultureNames.Clear();
            ValidateTextKeysDelayed();
            StatusText = Tx.T("statusbar.n files loaded", filesLoaded) + Tx.T("statusbar.n text keys defined", TextKeys.Count);
            UpdateTitle();
            ClearViewHistory();
        }

		private void OnSave()
		{
			Save();
		}

		private bool Save()
		{
		    foreach (var rootKey in RootKeys)
		    {
                var serializer = rootKey.Serializer ?? SerializeProvider.Instance.Version2;
                ISerializeLocation newLocation = null;
                if (rootKey.Location == null)
                {
                    // Ask for new file name and version
                    var dlg = new SaveFileDialog
                    {
                        AddExtension = true,
                        CheckPathExists = true,
                        DefaultExt = ".txd",
                        Filter = Tx.T("file filter.tx dictionary files") + " (*.txd)|*.txd|" +
                                 Tx.T("file filter.xml files") + " (*.xml)|*.xml|" +
                                 Tx.T("file filter.all files") + " (*.*)|*.*",
                        OverwritePrompt = true,
                        Title = Tx.T("msg.save.title"),
                        FileName = rootKey.DisplayName
                    };
                    if (dlg.ShowDialog(MainWindow.Instance) == true)
                    {
                        newLocation = new FileLocation(dlg.FileName);
                        if (Path.GetExtension(dlg.FileName) == ".xml") serializer = SerializeProvider.Instance.Version1;
                    }
                    else
                    {
                        return false;
                    }
                }
                else if (Equals(serializer, SerializeProvider.Instance.Version1))
                {
                    // Saving existing format 1 file.
                    // Ask to upgrade to version 2 format.

                    if (App.Settings.File.AskSaveUpgrade)
                    {
                        var result = TaskDialog.Show(
                            owner: MainWindow.Instance,
                            allowDialogCancellation: true,
                            title: "TxEditor",
                            mainInstruction: Tx.T("msg.save.upgrade to format 2"),
                            customButtons: new string[] { Tx.T("task dialog.button.upgrade"), Tx.T("task dialog.button.save in original format") },
                            verificationText: Tx.T("msg.save.upgrade to format 2.dont ask again"));

                        if (result.CustomButtonResult == null)
                        {
                            return false;
                        }
                        if (result.CustomButtonResult == 0)
                        {
                            serializer = SerializeProvider.Instance.Version2;
                        }
                        if (result.VerificationChecked == true)
                        {
                            // Remember to not ask again
                            App.Settings.File.AskSaveUpgrade = false;
                        }
                    }
                }

                if (Equals(serializer, SerializeProvider.Instance.Version1))
                {
                    // Check for usage of version 2 features
                    var foundIncompatibleFeatures = false;
                    Action<TextKeyViewModel> checkTextKey = null;
                    checkTextKey = vm =>
                    {
                        foreach (var ct in vm.CultureTextVMs)
                        {
                            // Find modulo values and new placeholders {#} and {=...}
                            if (ct.Text != null && Regex.IsMatch(ct.Text, @"(?<!\{)\{(?:#\}|=)")) foundIncompatibleFeatures = true;

                            foreach (var qt in ct.QuantifiedTextVMs)
                            {
                                if (qt.Modulo != 0) foundIncompatibleFeatures = true;
                                if (qt.Text != null && Regex.IsMatch(qt.Text, @"(?<!\{)\{(?:#\}|=)")) foundIncompatibleFeatures = true;
                            }
                        }

                        foreach (var child in vm.Children.Enumerate<TextKeyViewModel>())
                        {
                            checkTextKey(child);
                        }
                    };
                    checkTextKey(rootKey);

                    if (foundIncompatibleFeatures)
                    {
                        var result = TaskDialog.Show(
                            owner: MainWindow.Instance,
                            allowDialogCancellation: true,
                            title: "TxEditor",
                            mainInstruction: Tx.T("msg.save.incompatible with format 1"),
                            content: Tx.T("msg.save.incompatible with format 1.desc"),
                            customButtons: new[] { Tx.T("task dialog.button.save anyway"), Tx.T("task dialog.button.dont save") });

                        if (result.CustomButtonResult != 0)
                        {
                            return false;
                        }
                    }
                }

                var translation = DumpTranslation(rootKey);
                var location = newLocation ?? rootKey.Location;

                if (!App.SaveTo(translation, serializer, location)) return false;

                rootKey.Serializer = serializer;
                rootKey.Location = location;
                rootKey.HasUnsavedChanges = false;
            }
            
			StatusText = Tx.T("statusbar.file saved");
			return true;
		}

		private void OnImportFile()
		{
            var dlg = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = Tx.T("file filter.tx dictionary files") + " (*.txd)|*.txd|" +
                         Tx.T("file filter.xml files") + " (*.xml)|*.xml|" +
                         Tx.T("file filter.all files") + " (*.*)|*.*",
                Multiselect = true,
                ShowReadOnly = false,
                Title = Tx.T("msg.import file.title")
            };
            if (dlg.ShowDialog(MainWindow.Instance) != true) return;

		    var translationDlg = new TranslationSelectWindow
		    {
		        Title = Tx.T("window.select translation.import.title"),
		        CaptionLabel =
		        {
		            Text = Tx.T("window.select translation.import.caption")
		        },
		        OKButton =
		        {
		            Content = Tx.T("window.select translation.import.accept button")
		        }
		    };

            translationDlg.RootItems.DisplayMemberPath = "DisplayName";

            foreach (var rootKey in RootKeys)
            {
                translationDlg.RootItems.Items.Add(rootKey);
            }
            translationDlg.RootItems.Items.Add(new CreateNewTranslationObject());


            var selectedKey = MainWindow.Instance.TextKeysTreeView.LastSelectedItem as TextKeyViewModel;
		    if (selectedKey != null) translationDlg.RootItems.SelectedItem = selectedKey.FindRoot();

            translationDlg.RootItems.SelectedItem = translationDlg.RootItems.SelectedItem ?? translationDlg.RootItems.Items.Enumerate().FirstOrDefault();
            if (translationDlg.ShowDialog() != true) return;

		    var selectedRoot = translationDlg.RootItems.SelectedItem as RootKeyViewModel;

            if (selectedRoot == null)
            {
                selectedRoot = new RootKeyViewModel(this);
                RootKeys.Add(selectedRoot);
            }

            var successfullyImportedFiles = dlg.FileNames.Where(fileName =>
            {
                try
                {
                    var location = new FileLocation(fileName);
                    var serializeProvider = SerializeProvider.Instance;
                    var serializer = serializeProvider.DetectSerializer(location);
                    var instruction = serializeProvider.LoadFrom(location, serializer);
                    return Import(selectedRoot, instruction) == DialogResult.OK;
                }
                catch (Exception)
                {
                    //Log import error
                    return false;
                }
            }).ToArray();

            if (!successfullyImportedFiles.Any()) return;

            SortCulturesInTextKey(selectedRoot);
            ValidateTextKeysDelayed();
            StatusText = Tx.T("statusbar.n locations imported", successfullyImportedFiles.Length) + Tx.T("statusbar.n text keys defined", TextKeys.Count);
            selectedRoot.HasUnsavedChanges = true;
        }

		private bool CanExportKeys()
		{
			return selectedTextKeys != null && selectedTextKeys.Count > 0;
		}

		private void OnExportKeys()
		{
		    var root = GetSelectedRoot();
            // Ask for new file name and version
            var dlg = new SaveFileDialog
		    {
		        AddExtension = true,
		        CheckPathExists = true,
		        DefaultExt = ".txd",
		        Filter = Tx.T("file filter.tx dictionary files") + " (*.txd)|*.txd|" +
		                 Tx.T("file filter.all files") + " (*.*)|*.*",
		        OverwritePrompt = true,
		        Title = Tx.T("msg.export.title")
		    };
            
		    if (dlg.ShowDialog(MainWindow.Instance) == true)
			{
                var serializer = root.Serializer ?? SerializeProvider.Instance.Version2;

                var translation = DumpTranslation(root);
			    translation.IsTemplate = false;

			    if (App.SaveTo(translation, serializer, new FileLocation(dlg.FileName))) StatusText = Tx.T("statusbar.exported");
			}
		}

		#endregion File section

		#region Culture section

		private void OnNewCulture()
		{
			var win = new CultureWindow();
			win.Owner = MainWindow.Instance;
		    if (win.ShowDialog() != true) return;

		    var ci = new CultureInfo(win.CodeText.Text);

		    AddNewCulture(ci.IetfLanguageTag, true, RootKeys.Cast<TextKeyViewModel>().ToArray());
            foreach (var root in RootKeys)
		    {
                if (win.InsertSystemKeysCheckBox.IsChecked == true) InsertSystemKeys(root, ci.Name);
                root.HasUnsavedChanges = true;
            }

            // Make the very first culture the primary culture by default
            if (LoadedCultureNames.Count == 1) PrimaryCulture = ci.IetfLanguageTag;

            StatusText = Tx.T("statusbar.culture added", "name", CultureInfoName(ci));
		}

		private bool CanDeleteCulture()
		{
			return !string.IsNullOrEmpty(SelectedCulture);
		}

		private void OnDeleteCulture()
		{
			var ci = new CultureInfo(SelectedCulture);

			if (App.YesNoQuestion(Tx.T("msg.delete culture", "name", CultureInfoName(ci))))
			{
                foreach (var root in RootKeys)
                {
                    DeleteCulture(root, SelectedCulture, true);
                    root.HasUnsavedChanges = true;
                }
                
                StatusText = Tx.T("statusbar.culture deleted", "name", CultureInfoName(ci));
            }
		}

		private void OnReplaceCulture()
		{
			//SortCulturesInTextKey(RootTextKey);
			//ValidateTextKeys();
			//FileModified = true;
			//SetPrimaryCultureCommand.RaiseCanExecuteChanged();
		}

		private void OnInsertSystemKeys()
		{
			InsertSystemKeys(GetSelectedRoot(), SelectedCulture);
		}

		private void OnViewDateTimeFormats()
		{
			if (dateTimeWindow.IsClosed())   // Extension method, accepts null
			{
				dateTimeWindow = new DateTimeWindow();
				dateTimeWindow.Owner = MainWindow.Instance;
			}
			if (!string.IsNullOrEmpty(SelectedCulture))
			{
				dateTimeWindow.Culture = SelectedCulture;
			}
			else if (!string.IsNullOrEmpty(PrimaryCulture))
			{
				dateTimeWindow.Culture = PrimaryCulture;
			}
			else
			{
				return;
			}
			dateTimeWindow.Show();
		}

		private bool CanSetPrimaryCulture()
		{
			return !string.IsNullOrEmpty(SelectedCulture) && SelectedCulture != PrimaryCulture;
		}

		private void OnSetPrimaryCulture()
		{
			CultureInfo ci = new CultureInfo(SelectedCulture);
			string cultureName = CultureInfoName(ci);

			var result = TaskDialog.Show(
				owner: MainWindow.Instance,
				title: "TxEditor",
				mainInstruction: Tx.T("msg.set primary culture", "name", cultureName),
				content: Tx.T("msg.set primary culture.desc"),
				customButtons: new string[] { Tx.T("task dialog.button.switch"), Tx.T("task dialog.button.cancel") },
				allowDialogCancellation: true);

			if (result.CustomButtonResult == 0)
			{
				PrimaryCulture = SelectedCulture;
			    foreach (var root in RootKeys)
			    {
                    SortCulturesInTextKey(root);
                    root.HasUnsavedChanges = true;
                }
				
				ValidateTextKeysDelayed();
                
				StatusText = Tx.T("statusbar.primary culture set", "name", CultureInfoName(ci));
				SetPrimaryCultureCommand.RaiseCanExecuteChanged();
			}
		}

		#endregion Culture section

		#region Text key section

		private void OnNewTextKey()
		{
		    var win = new TextKeyWindow
		    {
		        Owner = MainWindow.Instance,
		        Title = Tx.T("window.text key.create.title"),
		        CaptionLabel =
		        {
		            Text = Tx.T("window.text key.create.caption")
		        },
		        OKButton =
		        {
		            Content = Tx.T("window.text key.create.accept button")
		        }
		    };

		    win.RootItems.DisplayMemberPath = "DisplayName";

            foreach (var rootKey in RootKeys)
		    {
                win.RootItems.Items.Add(rootKey);
            }
		    win.RootItems.Items.Add(new CreateNewTranslationObject());


            var selectedKey = MainWindow.Instance.TextKeysTreeView.LastSelectedItem as TextKeyViewModel;
		    if (selectedKey != null)
		    {
		        win.TextKey = selectedKey.TextKey + (selectedKey.IsNamespace ? ":" : ".");
		        win.RootItems.SelectedItem = selectedKey.FindRoot();
		    }

		    win.RootItems.SelectedItem = win.RootItems.SelectedItem ?? win.RootItems.Items.Enumerate().FirstOrDefault();

            if (win.ShowDialog() != true) return;

		    var newKey = win.TextKey;

		    TextKeyViewModel tk;
		    try
		    {
		        tk = FindOrCreateTextKey(win.RootItems.SelectedItem as RootKeyViewModel, newKey);
		    }
		    catch (NonNamespaceExistsException)
		    {
		        App.WarningMessage(Tx.T("msg.cannot create namespace key", "key", Tx.Q(newKey)));
		        return;
		    }
		    catch (NamespaceExistsException)
		    {
		        App.WarningMessage(Tx.T("msg.cannot create non-namespace key", "key", Tx.Q(newKey)));
		        return;
		    }

		    bool alreadyExists = !tk.IsEmpty();

		    // Ensure that all loaded cultures exist in every text key so that they can be entered
		    foreach (string cn in LoadedCultureNames)
		    {
		        EnsureCultureInTextKey(tk, cn);
		    }
		    tk.UpdateCultureTextSeparators();

		    ValidateTextKeysDelayed();
            tk.FindRoot().HasUnsavedChanges = true;

		    bool wasExpanded = tk.IsExpanded;
		    tk.IsExpanded = true;   // Expands all parents
		    if (!wasExpanded)
		        tk.IsExpanded = false;   // Collapses this item again
		    ViewCommandManager.InvokeLoaded("SelectTextKey", tk);

		    if (alreadyExists)
		    {
		        StatusText = Tx.T("statusbar.text key already exists");
		    }
		    else
		    {
		        StatusText = Tx.T("statusbar.text key created");
		    }

		    if (tk.CultureTextVMs.Count > 0)
		        tk.CultureTextVMs[0].ViewCommandManager.InvokeLoaded("FocusText");
		}

		// TODO: This is not a command handler, move it elsewhere
		public void TextKeySelectionChanged(IList selectedItems)
		{
			selectedTextKeys = selectedItems.OfType<TextKeyViewModel>().ToList();
			ExportKeysCommand.RaiseCanExecuteChanged();
			DeleteTextKeyCommand.RaiseCanExecuteChanged();
			RenameTextKeyCommand.RaiseCanExecuteChanged();
			DuplicateTextKeyCommand.RaiseCanExecuteChanged();
			AppendViewHistory();
			UpdateNavigationButtons();
			UpdateSuggestionsLater();

			HaveComment = false;
			foreach (TextKeyViewModel tk in selectedTextKeys)
			{
				HaveComment |= !string.IsNullOrWhiteSpace(tk.Comment);
			}
		}

		private bool CanDeleteTextKey()
		{
			return selectedTextKeys != null && selectedTextKeys.Count > 0;
		}

		private void OnDeleteTextKey()
		{
			if (selectedTextKeys == null || selectedTextKeys.Count == 0) return;

			var count = 0;
			var onlyFullKeysSelected = true;
			foreach (var tk in selectedTextKeys)
			{
				// TODO: Check whether any selected key is a child of another selected key -> don't count them additionally - collect all selected keys in a HashSet, then count
				// or use TreeViewItemViewModel.IsAParentOf method
				count += CountTextKeys(tk);
				if (!tk.IsFullKey)
					onlyFullKeysSelected = false;
			}
			if (count == 0)
			{
				// Means there were nodes with no full keys, should not happen
				FL.Warning("MainViewModel.OnDeleteTextKey: count == 0 (should not happen)");
				return;
			}

			TaskDialogResult result;
			var selectedOnlyOption = false;
			if (count == 1)
			{
				result = TaskDialog.Show(
					owner: MainWindow.Instance,
					allowDialogCancellation: true,
					title: "TxEditor",
					mainInstruction: Tx.T("msg.delete text key", "key", Tx.Q(lastCountedTextKey)),
					content: Tx.T("msg.delete text key.content"),
					customButtons: new[] { Tx.T("task dialog.button.delete"), Tx.T("task dialog.button.cancel") });
			}
			else if (onlyFullKeysSelected && selectedTextKeys.Count < count)
			{
				result = TaskDialog.Show(
					owner: MainWindow.Instance,
					allowDialogCancellation: true,
					title: "TxEditor",
					mainInstruction: Tx.T("msg.delete text key.multiple", count),
					content: Tx.T("msg.delete text key.multiple.content mixed"),
					radioButtons: new[] { Tx.T("msg.delete text key.multiple.also subkeys"), Tx.T("msg.delete text key.multiple.only selected") },
					customButtons: new[] { Tx.T("task dialog.button.delete"), Tx.T("task dialog.button.cancel") });
				selectedOnlyOption = result.RadioButtonResult == 1;
			}
			else
			{
				result = TaskDialog.Show(
					owner: MainWindow.Instance,
					allowDialogCancellation: true,
					title: "TxEditor",
					mainInstruction: Tx.T("msg.delete text key.multiple", count),
					content: Tx.T("msg.delete text key.multiple.content"),
					customButtons: new[] { Tx.T("task dialog.button.delete"), Tx.T("task dialog.button.cancel") });
			}
			if (result.CustomButtonResult == 0)
			{
				// Determine the remaining text key to select after deleting
				var lastSelectedTk = selectedTextKeys[selectedTextKeys.Count - 1];
				var remainingItem = lastSelectedTk.FindRemainingItem(t => !selectedTextKeys.Contains(t) && !selectedTextKeys.Any(s => s.IsAParentOf(t)));

				var isAnySelectedRemaining = false;
				foreach (var tk in selectedTextKeys.ToArray())
				{
					DeleteTextKey(tk, !selectedOnlyOption);
					// Also remove unused partial keys
					DeletePartialParentKeys(tk.Parent as TextKeyViewModel);
					if (tk.Parent.Children.Contains(tk)) isAnySelectedRemaining = true;
				    var root = tk.FindRoot();
                    if(root != null) root.HasUnsavedChanges = true;
                }
                
                if (!isAnySelectedRemaining)
				{
					// Select and focus other key in the tree
					ViewCommandManager.InvokeLoaded("SelectTextKey", remainingItem);
				}
				ValidateTextKeysDelayed();

				StatusText = Tx.T("statusbar.n text keys deleted", count);
			}
		}

		private string lastCountedTextKey;

		/// <summary>
		/// Counts all full keys within the specified subtree, including the specified text key.
		/// </summary>
		/// <param name="tk">Text key to start counting at.</param>
		/// <returns></returns>
		private int CountTextKeys(TextKeyViewModel tk)
		{
			int count = tk.IsFullKey ? 1 : 0;
			if (tk.IsFullKey)
				lastCountedTextKey = tk.TextKey;
			foreach (TextKeyViewModel child in tk.Children.Enumerate<TextKeyViewModel>())
			{
				count += CountTextKeys(child);
			}
			return count;
		}

		private void DeleteTextKey(TextKeyViewModel tk, bool includeChildren = true)
		{
			if (includeChildren)
			{
				foreach (var child in tk.Children.Enumerate<TextKeyViewModel>().ToArray())
				{
					DeleteTextKey(child);
				}
			}
			if (tk.IsFullKey)
			{
			    List<TextKeyViewModel> keys;
			    if (TextKeys.TryGetValue(tk.TextKey, out keys)) keys.Remove(tk);
				ProblemKeys.Remove(tk);
			}
			if (tk.Children.Count == 0)
			{
				tk.Parent.Children.Remove(tk);
			}
			else
			{
				tk.IsFullKey = false;
				tk.CultureTextVMs.Clear();
				tk.Comment = null;
				tk.Validate();
				OnPropertyChanged("SelectionDummy");
			}
		}

		/// <summary>
		/// Searches up the tree parents, starting from the specified key, and deletes the
		/// top-most unused text key, i.e. a key that is partial and has no children.
		/// </summary>
		/// <param name="tk"></param>
		private void DeletePartialParentKeys(TextKeyViewModel tk)
		{
			TextKeyViewModel nodeToDelete = null;
			TextKeyViewModel current = tk;

			while (true)
			{
				if (current == null)
				{
					// No more parents
					break;
				}
				if (current is RootKeyViewModel)
				{
					// Don't try to delete the root key
					break;
				}
				if (current.IsFullKey || CountTextKeys(current) > 0)
				{
					// The current key is not unused
					break;
				}
				nodeToDelete = current;
				current = current.Parent as TextKeyViewModel;
			}
			if (nodeToDelete != null)
			{
				DeleteTextKey(nodeToDelete);
			}
		}

		private void OnTextKeyWizard()
		{
		    var win = new TextKeyWizardWindow
		    {
		        Owner = MainWindow.Instance
		    };

		    if (win.ShowDialog() == true)
			{
				HandleWizardInput(win.TextKeyText.Text, win.TranslationText.Text);
			}
		}

		private IntPtr fgWin;
		private IDataObject clipboardBackup;

		public void TextKeyWizardFromHotKey()
		{
			string fileExtension = null;

			// Determine the currently active window
			fgWin = WinApi.GetForegroundWindow();

			// Require it to be Visual Studio, otherwise do nothing more
			if (App.Settings.Wizard.HotkeyInVisualStudioOnly)
			{
				StringBuilder sb = new StringBuilder(1000);
				WinApi.GetWindowText(fgWin, sb, 1000);
				if (!sb.ToString().EndsWith(" - Microsoft Visual Studio")) return;

				// Find active document file name
				try
				{
					var focusedElement = System.Windows.Automation.AutomationElement.FocusedElement;
					if (focusedElement != null)
					{
						var treeWalker = System.Windows.Automation.TreeWalker.ControlViewWalker;
						var parent = treeWalker.GetParent(focusedElement);
						while (parent != null &&
							(parent.Current.ControlType != System.Windows.Automation.ControlType.Pane ||
							parent.Current.ClassName != "ViewPresenter" ||
							string.IsNullOrEmpty(parent.Current.Name)))
						{
							parent = treeWalker.GetParent(parent);
						}
						if (parent != null)
						{
							string fileName = parent.Current.Name.TrimEnd('*');
							fileExtension = Path.GetExtension(fileName).ToLowerInvariant();
						}
					}
				}
				catch (Exception ex)
				{
					FL.Warning(ex, "Getting UI Automation info");
				}
			}

			// Backup current clipboard content
			clipboardBackup = ClipboardHelper.GetDataObject();

			// Send Ctrl+C keys to the active window to copy the selected text
			// (First send events to release the still-pressed hot key buttons Ctrl and Shift)
			WinApi.INPUT[] inputs = {
				new WinApi.INPUT { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.VK.CONTROL, dwFlags = WinApi.KEYEVENTF_KEYUP } },
				new WinApi.INPUT { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.VK.SHIFT, dwFlags = WinApi.KEYEVENTF_KEYUP } },
				new WinApi.INPUT { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.VK.CONTROL } },
				new WinApi.INPUT { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.KeyToVk(System.Windows.Forms.Keys.C) } },
				new WinApi.INPUT { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.KeyToVk(System.Windows.Forms.Keys.C), dwFlags = WinApi.KEYEVENTF_KEYUP } },
				new WinApi.INPUT { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.VK.CONTROL, dwFlags = WinApi.KEYEVENTF_KEYUP } },
			};
			uint ret = WinApi.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinApi.INPUT)));
			//System.Diagnostics.Debug.WriteLine(ret + " inputs sent");

			DelayedCall.Start(() => TextKeyWizardFromHotKey2(fileExtension), 50);
		}

		private void TextKeyWizardFromHotKey2(string fileExtension)
		{
			// Create the wizard window
			TextKeyWizardWindow win = new TextKeyWizardWindow();
			//win.Owner = MainWindow.Instance;
			win.ShowInTaskbar = true;
			win.ClipboardBackup = clipboardBackup;

			// Set the correct source cod language if we know the editor file name
			switch (fileExtension)
			{
				case ".aspx":
					win.SourceAspxButton.IsChecked = true;
					break;
				case ".cs":
					win.SourceCSharpButton.IsChecked = true;
					break;
				case ".cshtml":
					win.SourceCSharpButton.IsChecked = true;
					win.SourceHtmlButton.IsChecked = true;
					break;
				case ".xaml":
					win.SourceXamlButton.IsChecked = true;
					break;
			}

			MainWindow.Instance.Hide();

			bool ok = false;
			if (win.ShowDialog() == true)
			{
				ok = HandleWizardInput(win.TextKeyText.Text, win.TranslationText.Text);
			}

			// Delay showing TxEditor window to avoid flickering over Visual Studio window.
			// Since the window is focused when shown, it must be shown immediately anyway.
			// The trick here is to move it way off-screen first and fetch it back a bit later.
			MainWindow.Instance.Top -= 10000;
			MainWindow.Instance.Show();
			DelayedCall.Start(() => MainWindow.Instance.Top += 10000, 100);
			// Activate the window we're initially coming from
			WinApi.SetForegroundWindow(fgWin);

			if (ok)
			{
				// Send Ctrl+V keys to paste the new Tx call with the text key,
				// directly replacing the literal string that was selected before
				WinApi.INPUT[] inputs = new WinApi.INPUT[]
				{
					new WinApi.INPUT() { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.VK.CONTROL } },
					new WinApi.INPUT() { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.KeyToVk(System.Windows.Forms.Keys.V) } },
					new WinApi.INPUT() { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.KeyToVk(System.Windows.Forms.Keys.V), dwFlags = WinApi.KEYEVENTF_KEYUP } },
					new WinApi.INPUT() { type = WinApi.INPUT_KEYBOARD, ki = new WinApi.KEYBDINPUT() { wVk = (short) WinApi.VK.CONTROL, dwFlags = WinApi.KEYEVENTF_KEYUP } },
				};
				uint ret = WinApi.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf(typeof(WinApi.INPUT)));
			}

			clipboardBackup = win.ClipboardBackup;
			if (clipboardBackup != null)
			{
				DelayedCall.Start(TextKeyWizardFromHotKey3, 500);
			}
		}

		private void TextKeyWizardFromHotKey3()
		{
			// Restore clipboard
			Clipboard.SetDataObject(clipboardBackup, true);
		}

		private bool HandleWizardInput(string keyName, string text)
		{
			//TODO: Handle this later
            //TextKeyViewModel tk;
			//try
			//{
			//	tk = FindOrCreateTextKey(keyName);
			//}
			//catch (NonNamespaceExistsException)
			//{
			//	App.WarningMessage(Tx.T("msg.cannot create namespace key", "key", Tx.Q(keyName)));
			//	return false;
			//}
			//catch (NamespaceExistsException)
			//{
			//	App.WarningMessage(Tx.T("msg.cannot create non-namespace key", "key", Tx.Q(keyName)));
			//	return false;
			//}

			//bool alreadyExists = !tk.IsEmpty();

			//// Ensure that all loaded cultures exist in every text key so that they can be entered
			//foreach (string cn in LoadedCultureNames)
			//{
			//	EnsureCultureInTextKey(tk, cn);
			//}
			//tk.UpdateCultureTextSeparators();

			//// Set the text for the new key
			//tk.CultureTextVMs[0].Text = text;

			//ValidateTextKeysDelayed();
   //         RootTextKey.HasUnsavedChanges = true;

			//if (alreadyExists)
			//{
			//	StatusText = Tx.T("statusbar.text key already exists");
			//}
			//else
			//{
			//	StatusText = Tx.T("statusbar.text key added");
			//}

			//bool wasExpanded = tk.IsExpanded;
			//tk.IsExpanded = true;   // Expands all parents
			//if (!wasExpanded)
			//	tk.IsExpanded = false;   // Collapses the item again like it was before
			//ViewCommandManager.InvokeLoaded("SelectTextKey", tk);
			//return true;
		    return false;
		}

		private bool CanRenameTextKey()
		{
			return selectedTextKeys != null && selectedTextKeys.Count == 1;
		}

		private void OnRenameTextKey()
		{
			var selKey = MainWindow.Instance.TextKeysTreeView.LastSelectedItem as TextKeyViewModel;
			if (selKey == null) return;   // No key selected, something is wrong
            var selKeyAsRoot = selKey as RootKeyViewModel;

            var win = new TextKeyWindow
		    {
		        Owner = MainWindow.Instance,
		        Title = Tx.T("window.text key.rename.title"),
		        CaptionLabel =
		        {
		            Text = Tx.T("window.text key.rename.caption")
		        },
		        TextKey = selKeyAsRoot != null ? selKeyAsRoot.DisplayName : selKey.TextKey,
		        OKButton =
		        {
		            Content = Tx.T("window.text key.rename.accept button")
		        },
		        RenameSelectMode = true
		    };

		    if (selKeyAsRoot != null)
		    {
		        win.RootItems.Visibility = Visibility.Collapsed;
		    }

		    win.RootItems.DisplayMemberPath = "DisplayName";

            foreach (var rootKey in RootKeys)
            {
                win.RootItems.Items.Add(rootKey);
            }

            win.RootItems.Items.Add(new CreateNewTranslationObject());
		    var selKeyRoot = selKey.FindRoot();
		    win.RootItems.SelectedItem = selKeyRoot ?? win.RootItems.Items.Enumerate().FirstOrDefault();

            if (selKey.Children.Count > 0 && selKeyAsRoot == null)
			{
				// There are other keys below the selected key
				// Initially indicate that all subkeys will also be renamed
				win.IncludeSubitemsCheckbox.Visibility = Visibility.Visible;
				win.IncludeSubitemsCheckbox.IsChecked = true;
				win.IncludeSubitemsCheckbox.IsEnabled = false;

				if (selKey.IsFullKey)
				{
					// The selected key is a full key
					// Allow it to be renamed independently of the subkeys
					win.IncludeSubitemsCheckbox.IsEnabled = true;
				}
			}

			if (win.ShowDialog() == true)
			{
				// The dialog was confirmed
				string newKey = win.TextKey;


			    if (selKeyAsRoot != null)
			    {
			        selKeyAsRoot.DisplayName = newKey;
			        selKeyAsRoot.HasUnsavedChanges = true;
                    return;
			    }

                var newRoot = win.RootItems.SelectedItem as RootKeyViewModel;

                // Was the name changed at all?
                if (newKey == selKey.TextKey && selKeyRoot == newRoot) return;

				// Don't allow namespace nodes to be moved elsewhere
				if (selKey.IsNamespace && (newKey.Contains('.') || newKey.Contains(':')))
				{
					App.WarningMessage(Tx.T("msg.cannot move namespace"));
					return;
				}

				bool needDuplicateForChildren = win.IncludeSubitemsCheckbox.IsChecked == false && selKey.Children.Count > 0;
                

                // Test whether the entered text key already exists with content or subkeys
                TextKeyViewModel tryDestKey;
				try
				{
				    tryDestKey = FindOrCreateTextKey(newRoot, newKey, false, false);
				}
				catch (NonNamespaceExistsException)
				{
					App.WarningMessage(Tx.T("msg.cannot create namespace key", "key", Tx.Q(newKey)));
					return;
				}
				catch (NamespaceExistsException)
				{
					App.WarningMessage(Tx.T("msg.cannot create non-namespace key", "key", Tx.Q(newKey)));
					return;
				}
				bool destExists = tryDestKey != null && (!tryDestKey.IsEmpty() || tryDestKey.Children.Count > 0);
				bool destWasFullKey = false;
				if (destExists)
				{
					// FindOrCreateTextKey below will make it a full key, no matter whether it
					// should be one. Remember this state to reset it afterwards.
					destWasFullKey = tryDestKey.IsFullKey;

					TaskDialogResult result = TaskDialog.Show(
						owner: MainWindow.Instance,
						allowDialogCancellation: true,
						title: "TxEditor",
						mainInstruction: Tx.T("msg.rename text key.exists", "key", Tx.Q(newKey)),
						content: Tx.T("msg.rename text key.exists.content"),
						customButtons: new string[] { Tx.T("task dialog.button.merge"), Tx.T("task dialog.button.cancel") });
					if (result.CustomButtonResult != 0)
					{
						return;
					}
				}

				var oldParent = selKey.Parent;
				int affectedKeyCount = selKey.IsFullKey ? 1 : 0;

				if (!needDuplicateForChildren)
				{
					// Remove the selected key from its original tree position
					oldParent.Children.Remove(selKey);
				}

				// Create the new text key if needed
				var destKey = FindOrCreateTextKey(newRoot, newKey, false);
			    newRoot = destKey.FindRoot();
                if (!destExists)
				{
					// Key was entirely empty or is newly created.

					if (needDuplicateForChildren)
					{
						// Keep new key but copy all data from the source key
						destKey.MergeFrom(selKey);
						// The source key is now no longer a full key
						selKey.IsFullKey = false;
					}
					else
					{
						// Replace it with the source key
						affectedKeyCount = selKey.SetKeyRecursive(newKey, TextKeys);

						if (selKey.IsNamespace)
						{
							// We're renaming a namespace item. But we've created a temporary
							// normal key (destKey) which is now at the wrong position.
							// Namespace entries are sorted differently, which was not known when
							// creating the key because it was no namespace at that time. Remove the
							// newly created key entry (all of its possibly created parent keys are
							// still useful though!) and insert the selected key at the correct
							// position in that tree level.
							destKey.Parent.Children.Remove(destKey);
							destKey.Parent.Children.InsertSorted(selKey, TextKeyViewModel.Compare);
						}
						else
						{
							// The sort order is already good for normal keys so we can simply replace
							// the created item with the selected key at the same position.
							destKey.Parent.Children.Replace(destKey, selKey);
						}
						// Update the key's parent reference to represent the (possible) new tree location.
						selKey.Parent = destKey.Parent;
					}
				}
				else
				{
					// Key already has some text or child keys.

					// Restore original full key state first
					destKey.IsFullKey = destWasFullKey;
					// Merge data into destKey, overwriting conflicts
					destKey.MergeFrom(selKey);

					if (win.IncludeSubitemsCheckbox.IsChecked == true)
					{
						// Add/merge all subkeys as well
						destKey.MergeChildrenRecursive(selKey);
						// Delete the source key after it has been merged into destKey
						DeleteTextKey(selKey);
					}
					else
					{
						// The source key will be kept but is now no longer a full key
						selKey.IsFullKey = false;
                        List<TextKeyViewModel> keys;
                        if (TextKeys.TryGetValue(selKey.TextKey, out keys)) keys.Remove(selKey);
					}
				}

				if (!needDuplicateForChildren && oldParent != selKey.Parent)
				{
					// The key has moved to another subtree.
					// Clean up possible unused partial keys at the old position.
					DeletePartialParentKeys(oldParent as TextKeyViewModel);
				}

			    selKeyRoot.HasUnsavedChanges = true;
                newRoot.HasUnsavedChanges = true;

				StatusText = Tx.T("statusbar.text keys renamed", affectedKeyCount);

				// Fix an issue with MultiSelectTreeView: It can only know that an item is selected
				// when its TreeViewItem property IsSelected is set through a binding defined in
				// this application. The already-selected item was removed from the SelectedItems
				// list when it was removed from the tree (to be re-inserted later). Not sure how
				// to fix this right.
				selKey.IsSelected = true;

				if (needDuplicateForChildren || destExists)
				{
					bool wasExpanded = selKey.IsExpanded;
					destKey.IsExpanded = true;   // Expands all parents
					if (!wasExpanded)
						destKey.IsExpanded = false;   // Collapses the item again like it was before
					ViewCommandManager.InvokeLoaded("SelectTextKey", destKey);
				}
				else
				{
					bool wasExpanded = selKey.IsExpanded;
					selKey.IsExpanded = true;   // Expands all parents
					if (!wasExpanded)
						selKey.IsExpanded = false;   // Collapses the item again like it was before
					ViewCommandManager.InvokeLoaded("SelectTextKey", selKey);
				}
				ValidateTextKeysDelayed();
			}
		}

		private bool CanDuplicateTextKey()
		{
			return selectedTextKeys != null && selectedTextKeys.Count == 1;
		}

		private void OnDuplicateTextKey()
		{
			var selKey = MainWindow.Instance.TextKeysTreeView.LastSelectedItem as TextKeyViewModel;
			if (selKey == null) return;   // No key selected, something is wrong

		    var win = new TextKeyWindow
		    {
		        Owner = MainWindow.Instance,
		        Title = Tx.T("window.text key.duplicate.title"),
		        CaptionLabel =
		        {
		            Text = Tx.T("window.text key.duplicate.caption")
		        },
		        TextKey = selKey.TextKey,
		        OKButton =
		        {
		            Content = Tx.T("window.text key.duplicate.accept button")
		        },
		        RenameSelectMode = true
		    };

            win.RootItems.DisplayMemberPath = "DisplayName";

            foreach (var rootKey in RootKeys)
            {
                win.RootItems.Items.Add(rootKey);
            }

            win.RootItems.Items.Add(new CreateNewTranslationObject());
            win.RootItems.SelectedItem = selKey.FindRoot() ?? win.RootItems.Items.Enumerate().FirstOrDefault();

            if (selKey.Children.Count > 0)
			{
				// There are other keys below the selected key
				// Initially indicate that all subkeys will also be duplicated
				win.IncludeSubitemsCheckbox.Visibility = Visibility.Visible;
				win.IncludeSubitemsCheckbox.IsChecked = true;
				win.IncludeSubitemsCheckbox.IsEnabled = false;

				if (selKey.IsFullKey)
				{
					// The selected key is a full key
					// Allow it to be duplicated independently of the subkeys
					win.IncludeSubitemsCheckbox.IsEnabled = true;
				}
			}

			if (win.ShowDialog() == true)
			{
				// The dialog was confirmed
				string newKey = win.TextKey;
				bool includeChildren = win.IncludeSubitemsCheckbox.IsChecked == true;

				// Don't allow namespace nodes to be copied elsewhere
				if (selKey.IsNamespace && (newKey.Contains('.') || newKey.Contains(':')))
				{
					App.WarningMessage(Tx.T("msg.cannot copy namespace"));
					return;
				}

                var newRoot = win.RootItems.SelectedItem as RootKeyViewModel;
                // Test whether the entered text key already exists with content or subkeys
                TextKeyViewModel tryDestKey;
				try
				{
					tryDestKey = FindOrCreateTextKey(newRoot, newKey, false, false, selKey.IsNamespace);
				}
				catch (NonNamespaceExistsException)
				{
					App.WarningMessage(Tx.T("msg.cannot create namespace key", "key", Tx.Q(newKey)));
					return;
				}
				catch (NamespaceExistsException)
				{
					App.WarningMessage(Tx.T("msg.cannot create non-namespace key", "key", Tx.Q(newKey)));
					return;
				}
				bool destExists = tryDestKey != null && (!tryDestKey.IsEmpty() || tryDestKey.Children.Count > 0);
				bool destWasFullKey = false;
				if (destExists)
				{
					// FindOrCreateTextKey below will make it a full key, no matter whether it
					// should be one. Remember this state to reset it afterwards.
					destWasFullKey = tryDestKey.IsFullKey;

					TaskDialogResult result = TaskDialog.Show(
						owner: MainWindow.Instance,
						allowDialogCancellation: true,
						title: "TxEditor",
						mainInstruction: Tx.T("msg.rename text key.exists", "key", Tx.Q(newKey)),
						content: Tx.T("msg.rename text key.exists.content"),
						customButtons: new string[] { Tx.T("task dialog.button.merge"), Tx.T("task dialog.button.cancel") });
					if (result.CustomButtonResult != 0)
					{
						return;
					}
				}

				int affectedKeys = selKey.IsFullKey ? 1 : 0;

				// Create the new text key if needed
				var destKey = FindOrCreateTextKey(newRoot, newKey, true, true, selKey.IsNamespace);
			    newRoot = destKey.FindRoot();

                // Restore original full key state first
                destKey.IsFullKey = destWasFullKey;
				if (!destWasFullKey && !selKey.IsFullKey)
				{
                    List<TextKeyViewModel> keys;
                    if (TextKeys.TryGetValue(destKey.TextKey, out keys)) keys.Remove(destKey);
                }
				// Merge data into destKey, overwriting conflicts
				destKey.MergeFrom(selKey);

				if (includeChildren)
				{
					if (!destExists)
					{
						// Key was entirely empty or is newly created.

						foreach (var child in selKey.Children.Enumerate<TextKeyViewModel>())
						{
							affectedKeys += DuplicateTextKeyRecursive(child, destKey);
						}
					}
					else
					{
						// Key already has some text or child keys.

						// Add/merge all subkeys as wells
						destKey.MergeChildrenRecursive(selKey);
					}
				}

                newRoot.HasUnsavedChanges = true;
				StatusText = Tx.T("statusbar.text keys duplicated", affectedKeys);

				destKey.IsSelected = true;

				bool wasExpanded = selKey.IsExpanded;
				destKey.IsExpanded = true;   // Expands all parents
				if (!wasExpanded)
					destKey.IsExpanded = false;   // Collapses the item again like it was before
				ViewCommandManager.InvokeLoaded("SelectTextKey", destKey);
				ValidateTextKeysDelayed();
			}
		}

		private int DuplicateTextKeyRecursive(TextKeyViewModel srcTextKey, TextKeyViewModel destParent)
		{
			var destKeyName = destParent.TextKey + (destParent.IsNamespace ? ":" : ".") + srcTextKey.PartialKey;
			var destKey = FindOrCreateTextKey(destParent.FindRoot(), destKeyName);
			destKey.MergeFrom(srcTextKey);

			var affectedKeys = srcTextKey.IsFullKey ? 1 : 0;
			foreach (var child in srcTextKey.Children.Enumerate<TextKeyViewModel>())
			{
				affectedKeys += DuplicateTextKeyRecursive(child, destKey);
			}
			return affectedKeys;
		}

		#endregion Text key section

		#region View section

		private bool CanNavigateBack()
		{
			return viewHistoryIndex > 0;
		}

		private void OnNavigateBack()
		{
			ViewHistoryBack();
			UpdateNavigationButtons();
		}

		private bool CanNavigateForward()
		{
			return viewHistoryIndex < viewHistory.Count - 1;
		}

		private void OnNavigateForward()
		{
			ViewHistoryForward();
			UpdateNavigationButtons();
		}

		private bool CanGotoDefinition()
		{
			return false;
		}

		private void OnGotoDefinition()
		{
		}

		private void UpdateNavigationButtons()
		{
			NavigateBackCommand.RaiseCanExecuteChanged();
			NavigateForwardCommand.RaiseCanExecuteChanged();
		}

		#endregion View section

		#region Filter section

		// Placeholder

		#endregion Filter section

		#region Application section

		private SettingsWindow settingsWindow;

		private void OnSettings()
		{
			if (settingsWindow == null || !settingsWindow.IsVisible)
			{
				settingsWindow = new SettingsWindow();
				settingsWindow.Owner = MainWindow.Instance;
				settingsWindow.Show();
			}
			else
			{
				settingsWindow.Close();
				settingsWindow = null;
			}
		}

		private void OnAbout()
		{
			var win = new AboutWindow();
			win.Owner = MainWindow.Instance;
			win.ShowDialog();
		}

		private void OnHelp()
		{
			string docFileName = Path.Combine(
				Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
				"Tx Documentation.pdf");

			try
			{
				System.Diagnostics.Process.Start(docFileName);
			}
			catch (Exception ex)
			{
				App.ErrorMessage(null, ex, "Opening documentation PDF file");
			}
		}

		private void OnLibFolder()
		{
			string libFolder = Path.Combine(
				Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location),
				"TxLib source code");

			try
			{
				System.Diagnostics.Process.Start(libFolder);
			}
			catch (Exception ex)
			{
				App.ErrorMessage(null, ex, "Opening source code directory");
			}
		}

		#endregion Application section

		#endregion Toolbar command handlers

		#region Context menu command handlers

		private bool CanConvertToNamespace()
		{
			return selectedTextKeys != null && selectedTextKeys.Count == 1 && !selectedTextKeys[0].IsNamespace;
		}

		private void OnConvertToNamespace()
		{
			var selKey = selectedTextKeys[0];

			if (selKey.IsFullKey)
			{
				App.WarningMessage(Tx.T("msg.convert to namespace.is full key", "key", Tx.Q(selKey.TextKey)));
				return;
			}
			if (!(selKey.Parent is RootKeyViewModel))
			{
				App.WarningMessage(Tx.T("msg.convert to namespace.not a root child", "key", Tx.Q(selKey.TextKey)));
				return;
			}

			selKey.IsNamespace = true;
			foreach (var child in selKey.Children.OfType<TextKeyViewModel>())
			{
				child.SetKeyRecursive(selKey.TextKey + ":" + child.PartialKey, TextKeys);
			}
			selKey.Parent.Children.Remove(selKey);
			selKey.Parent.Children.InsertSorted(selKey, TextKeyViewModel.Compare);

            selKey.FindRoot().HasUnsavedChanges = true;
			StatusText = Tx.T("statusbar.text key converted to namespace");

			ViewCommandManager.InvokeLoaded("SelectTextKey", selKey);
			ValidateTextKeysDelayed();
		}

		private bool CanConvertToTextKey()
		{
			return selectedTextKeys != null && selectedTextKeys.Count == 1 && selectedTextKeys[0].IsNamespace;
		}

		private void OnConvertToTextKey()
		{
			var selKey = selectedTextKeys[0];

			if (selKey.PartialKey.IndexOf('.') != -1)
			{
				App.WarningMessage(Tx.T("msg.convert to text key.contains point", "key", Tx.Q(selKey.TextKey)));
				return;
			}

			selKey.IsNamespace = false;
			foreach (var child in selKey.Children.OfType<TextKeyViewModel>())
			{
				child.SetKeyRecursive(selKey.TextKey + "." + child.PartialKey, TextKeys);
			}
			selKey.Parent.Children.Remove(selKey);
			selKey.Parent.Children.InsertSorted(selKey, TextKeyViewModel.Compare);
		    selKey.FindRoot().HasUnsavedChanges = true;
			StatusText = Tx.T("statusbar.namespace converted to text key");

			ViewCommandManager.InvokeLoaded("SelectTextKey", selKey);
			ValidateTextKeysDelayed();
		}

		#endregion Context menu command handlers

		#region Other command handlers

		private bool CanCopyTextKey()
		{
			return selectedTextKeys.Count > 0;
		}

		private void OnCopyTextKey()
		{
			string str = selectedTextKeys
				.Select(tk => tk.TextKey)
				.Aggregate((a, b) => a + Environment.NewLine + b);
			Clipboard.SetText(str);
			StatusText = Tx.T("statusbar.text key copied");
		}

		private void OnSelectPreviousTextKey()
		{
			ViewCommandManager.Invoke("SelectPreviousTextKey", LastSelectedCulture);
		}

		private void OnSelectNextTextKey()
		{
			ViewCommandManager.Invoke("SelectNextTextKey", LastSelectedCulture);
		}

		#endregion Other command handlers

		#region XML loading methods

        private DialogResult Import(RootKeyViewModel root, DeserializeInstruction instruction)
        {
            if (root == null) throw new ArgumentNullException(nameof(root));
            if (instruction == null) throw new ArgumentNullException(nameof(instruction));
            SerializedTranslation translation;
            try
            {
                translation = instruction.Deserialize();
            }
            catch (Exception ex)
            {
                FL.Error("Error loading", instruction.Location.ToString());
                FL.Error(ex, "Loading XML dictionary");
                var result = TaskDialog.Show(
                    owner: MainWindow.Instance,
                    allowDialogCancellation: true,
                    title: "TxEditor",
                    mainIcon: VistaTaskDialogIcon.Error,
                    mainInstruction: Tx.T("msg.load file.invalid location"),
                    content: Tx.T("msg.load file.invalid location.desc", "name", instruction.Location.ToString(), "msg", ex.Message),
                    customButtons: new[] { Tx.T("task dialog.button.skip file"), Tx.T("task dialog.button.cancel") });

                if (result.CustomButtonResult == 0) return DialogResult.Ignore;
                return DialogResult.Cancel;
            }

            foreach (var culture in translation.Cultures)
            {
                if (!LoadedCultureNames.Contains(culture.Name))
                {
                    var result = TaskDialog.Show(
                        owner: MainWindow.Instance,
                        allowDialogCancellation: true,
                        title: "TxEditor",
                        mainInstruction: Tx.T("msg.import location.add new culture", "culture", culture.Name),
                        content: Tx.T("msg.import location.add new culture.desc", "name", instruction.Location.ToString(), "culture", culture.Name),
                        customButtons: new[] 
                        {
                            Tx.T("task dialog.button.add culture"),
                            Tx.T("task dialog.button.skip culture"),
                            Tx.T("task dialog.button.cancel")
                        });

                    if (result.CustomButtonResult == 1) continue;
                    if (result.CustomButtonResult == 2) return DialogResult.Cancel;
                }

                ComposeKeys(root, culture.Name, culture.Keys);

                if (culture.IsPrimary)
                {
                    PrimaryCulture = culture.Name;
                }
            }
            

            return DialogResult.OK;
        }

        private void ComposeKeys(RootKeyViewModel root, string cultureName, IEnumerable<SerializedKey> keys)
		{
            if (root == null) throw new ArgumentNullException(nameof(root));
            // Add the new culture everywhere
            if (!LoadedCultureNames.Contains(cultureName))
            {
                AddNewCulture(cultureName, false, RootKeys.Union(new TextKeyViewModel[] { root }).Distinct().ToArray());
            }

	        var validKeys = keys.Enumerate().Where(k =>
		    {
		        string errorMessage;
		        return TextKeyViewModel.ValidateName(k.Key, out errorMessage);
		    });

            foreach (var validKey in validKeys)
		    {
                // TODO: Catch exceptions NonNamespaceExistsException and NamespaceExistsException for invalid files
                var tk = FindOrCreateTextKey(root, validKey.Key);
                if (validKey.Comment != null) tk.Comment = validKey.Comment;

                // Ensure that all loaded cultures exist in every text key so that they can be entered
                foreach (string cn in LoadedCultureNames)
                {
                    EnsureCultureInTextKey(tk, cn);
                }

                tk.UpdateCultureTextSeparators();

                // Find the current culture
                var ct = tk.CultureTextVMs.First(vm => vm.CultureName == cultureName);
                if (validKey.Count == -1)
                {
                    // Default text, store it directly in the item
                    ct.Text = validKey.Text;
                    ct.AcceptMissing = validKey.AcceptMissing;
                    ct.AcceptPlaceholders = validKey.AcceptPlaceholders;
                    ct.AcceptPunctuation = validKey.AcceptPunctuation;
                }
                else
                {
                    // Quantified text, go deeper
                    // Update existing entry or create and add a new one
                    var qt = ct.QuantifiedTextVMs.FirstOrDefault(q => q.Count == validKey.Count && q.Modulo == validKey.Modulo);

                    var newQt = qt == null;
                    if (qt == null)
                    {
                        qt = new QuantifiedTextViewModel(ct);
                        qt.Count = validKey.Count;
                        qt.Modulo = validKey.Modulo;
                    }
                    qt.Text = validKey.Text;
                    qt.AcceptMissing = validKey.AcceptMissing;
                    qt.AcceptPlaceholders = validKey.AcceptPlaceholders;
                    qt.AcceptPunctuation = validKey.AcceptPunctuation;
                    if (newQt) ct.QuantifiedTextVMs.InsertSorted(qt, QuantifiedTextViewModel.Compare);
                }
            }
            
		}

	    /// <summary>
	    /// Finds an existing TextKeyViewModel or creates a new one in the correct place.
	    /// </summary>
	    /// <param name="root">Root text key.</param>
	    /// <param name="textKey">The full text key to find or create.</param>
	    /// <param name="updateTextKeys">true to add the new text key to the TextKeys dictionary. (Only if <paramref name="create"/> is set.)</param>
	    /// <param name="create">true to create a new full text key if it doesn't exist yet, false to return null or partial TextKeyViewModels instead.</param>
	    /// <param name="isNamespace">true to indicate that a single key segment is meant to be a namespace key.</param>
	    /// <returns></returns>
	    private TextKeyViewModel FindOrCreateTextKey(RootKeyViewModel root, string textKey, bool updateTextKeys = true, bool create = true, bool isNamespace = false)
		{
	        if (root == null)
	        {
	            if (!create) return null;

                root = new RootKeyViewModel(this);
	            RootKeys.Add(root);
	        }
            
            // Tokenize text key to find the tree node
			string partialKey = "";
			TextKeyViewModel tk = root;
			if (!textKey.Contains(':') && isNamespace)
			{
				// Fake the separator to use existing code; clean up later
				textKey += ":";
			}
			string[] nsParts = textKey.Split(':');
			string localKey;
			if (nsParts.Length > 1)
			{
				// Namespace set
				partialKey = nsParts[0];
				var subtk = tk.Children.OfType<TextKeyViewModel>()
					.SingleOrDefault(vm => vm.PartialKey == nsParts[0]);
				if (subtk != null && !subtk.IsNamespace)
				{
					throw new NonNamespaceExistsException();
				}
				if (subtk == null)
				{
					// Namespace tree item does not exist yet, create it
					if (!create) return null;
					subtk = new TextKeyViewModel(nsParts[0], false, tk, tk.MainWindowVM);
					subtk.PartialKey = nsParts[0];
					subtk.IsNamespace = true;
					tk.Children.InsertSorted(subtk, TextKeyViewModel.Compare);
				}
				tk = subtk;
				// Continue with namespace-free text key
				localKey = nsParts[1];
				partialKey += ":";
			}
			else
			{
				// No namespace set, continue with entire key
				localKey = textKey;
			}

			if (localKey != "")
			{
				string[] keySegments = localKey.Split('.');
				for (int i = 0; i < keySegments.Length; i++)
				{
					string keySegment = keySegments[i];
					partialKey += keySegment;

					// Search for tree item
					var subtk = tk.Children.OfType<TextKeyViewModel>()
						.SingleOrDefault(vm => vm.PartialKey == keySegment);
					if (subtk != null && subtk.IsNamespace)
					{
						throw new NamespaceExistsException();
					}
					if (subtk == null)
					{
						// This level of text key item does not exist yet, create it
						if (!create) return null;
						subtk = new TextKeyViewModel(partialKey, i == keySegments.Length - 1, tk, tk.MainWindowVM);
						subtk.PartialKey = keySegment;
						tk.Children.InsertSorted(subtk, TextKeyViewModel.Compare);
					}
					tk = subtk;
					partialKey += ".";
				}
			}

			if (create)
			{
			    if (updateTextKeys) TextKeys.GetOrAdd(textKey, k => new List<TextKeyViewModel>()).Ensure(tk);
			    tk.IsFullKey = true;
			}
			return tk;
		}

        #endregion XML loading methods

        #region GetSystemTexts

        public Dictionary<string, Dictionary<string, Dictionary<int, string>>> GetSystemTexts(RootKeyViewModel root)
        {
            var result = new Dictionary<string, Dictionary<string, Dictionary<int, string>>>();
            var modelsWithKeys = root.FindViewModels(args =>
            {
                var textModel = args.Item as TextKeyViewModel;
                args.IncludeInResult = textModel?.IsFullKey == true && textModel.TextKey.StartsWith("Tx:");
            }).Cast<TextKeyViewModel>().ToArray();

            foreach (var textKeyViewModel in modelsWithKeys)
            {
                var textKey = textKeyViewModel.TextKey;

                foreach (var cultureTextViewModel in textKeyViewModel.CultureTextVMs)
                {
                    var cultureName = cultureTextViewModel.CultureName;
                    var countDictionary = result.GetOrAdd(cultureName).GetOrAdd(textKey);

                    if (!string.IsNullOrEmpty(cultureTextViewModel.Text))
                    {
                        countDictionary.AddOrUpgrade(-1, cultureTextViewModel.Text);
                    }
                    
                    foreach (var quantifiedTextViewModel in cultureTextViewModel.QuantifiedTextVMs)
                    {
                        if (quantifiedTextViewModel.Count < 0) continue;
                        if (quantifiedTextViewModel.Modulo != 0 && quantifiedTextViewModel.Modulo < 2 && quantifiedTextViewModel.Modulo > 1000) continue;
                        if (string.IsNullOrEmpty(quantifiedTextViewModel.Text)) continue;

                        var count = quantifiedTextViewModel.Count;
                        if (quantifiedTextViewModel.Modulo != 0)
                        {
                            // Encode the modulo value into the quantifier.
                            count = (quantifiedTextViewModel.Modulo << 16) | count;
                        }
                        countDictionary.AddOrUpgrade(count, quantifiedTextViewModel.Text);
                    }
                }
            }
            return result;
		}


        #endregion GetSystemTexts

        #region Text validation

        private DelayedCall validateDc;

		/// <summary>
		/// Validates all text keys and updates the suggestions later.
		/// </summary>
		public void ValidateTextKeysDelayed()
		{
			if (validateDc == null)
			{
				validateDc = DelayedCall.Start(ValidateTextKeys, 500);
			}
			else
			{
				validateDc.Reset();
			}
		}

		/// <summary>
		/// Validates all text keys now and updates the suggestions later.
		/// </summary>
		public void ValidateTextKeys()
		{
		    RootKeys.ForEach(k => k.Validate());
			UpdateSuggestionsLater();
		}

		#endregion Text validation

		#region Culture management

		private void EnsureCultureInTextKey(TextKeyViewModel tk, string cultureName)
		{
			if (tk.CultureTextVMs.All(vm => vm.CultureName != cultureName))
			{
				tk.CultureTextVMs.InsertSorted(new CultureTextViewModel(cultureName, tk), (a, b) => a.CompareTo(b));
			}
		}

	    private void AddNewCulture(string cultureName, bool validate, params TextKeyViewModel[] roots)
	    {
	        foreach (var root in roots)
	        {
	            foreach (var tk in root.Children.Enumerate<TextKeyViewModel>())
	            {
	                EnsureCultureInTextKey(tk, cultureName);
	                tk.UpdateCultureTextSeparators();
	            }
                AddNewCulture(cultureName, validate, root.Children.Enumerate<TextKeyViewModel>().ToArray());

	            if (!LoadedCultureNames.Contains(cultureName)) LoadedCultureNames.Add(cultureName);

	            DeletedCultureNames.Remove(cultureName); // in case it's been deleted before
	            if (validate) ValidateTextKeysDelayed();
	        }
	    }

	    private void DeleteCulture(TextKeyViewModel root, string cultureName, bool validate)
		{
			foreach (var tk in root.Children.Enumerate<TextKeyViewModel>())
			{
				tk.CultureTextVMs.Filter(ct => ct.CultureName != cultureName);
				tk.UpdateCultureTextSeparators();
				if (tk.Children.Count > 0)
				{
					DeleteCulture(tk, cultureName, validate);
				}
			}
			LoadedCultureNames.Remove(cultureName);
			if (!DeletedCultureNames.Contains(cultureName))
			{
				DeletedCultureNames.Add(cultureName);
			}
			if (validate)
			{
				ValidateTextKeysDelayed();
			}
		}

		private void SortCulturesInTextKey(TextKeyViewModel root)
		{
			foreach (var tk in root.Children.Enumerate<TextKeyViewModel>())
			{
				var ctList = tk.CultureTextVMs.ToArray();
				tk.CultureTextVMs.Clear();
				foreach (var ct in ctList)
				{
					tk.CultureTextVMs.InsertSorted(ct, (a, b) => a.CompareTo(b));
				}

				tk.UpdateCultureTextSeparators();
				if (tk.Children.Count > 0)
				{
					SortCulturesInTextKey(tk);
				}
			}
		}

		public static string CultureInfoName(CultureInfo ci, bool includeCode = true)
		{
			return Tx.U(App.Settings.View.NativeCultureNames ? ci.NativeName : ci.DisplayName) +
				(includeCode ? " [" + ci.IetfLanguageTag + "]" : "");
		}

		private void InsertSystemKeys(RootKeyViewModel root, string culture)
		{
            if(root == null) return;
		    
            if (string.IsNullOrEmpty(culture))
		    {
		        App.WarningMessage(Tx.T("msg.insert system keys.no culture selected"));
                return;
		    }

            var location = new EmbeddedResourceLocation(System.Reflection.Assembly.GetExecutingAssembly(), "Unclassified.TxEditor.Template.txd");
            var serializeProvider = SerializeProvider.Instance;

            var serializer = serializeProvider.DetectSerializer(location);
            var translation = serializeProvider.LoadFrom(location, serializer).Deserialize();

		    var existingCulture = translation.Cultures.FirstOrDefault(c => c.Name == culture);
		    if (existingCulture == null)
		    {
		        App.WarningMessage(Tx.T("msg.insert system keys.not available", "name", culture));
                return;
		    }

		    ComposeKeys(root, culture, existingCulture.Keys);

            root.HasUnsavedChanges = true;
		    StatusText = Tx.T("statusbar.system keys added", "culture", culture);

		    if (culture.Length == 5) App.InformationMessage(Tx.T("msg.insert system keys.base culture", "name", culture.Substring(0, 2)));
		}

	    #endregion Culture management

		#region Window management

		private void UpdateTitle()
		{
		    var formattedTitle = GetSelectedRoot()?.FormatTitle();
		    if (string.IsNullOrEmpty(formattedTitle)) DisplayName = "TxEditor";
		    else DisplayName = formattedTitle + " – TxEditor";
		}

		private void SelectTextKey(TextKeyViewModel tk, bool async = false)
		{
			if (tk != null)
			{
				bool wasExpanded = tk.IsExpanded;
				tk.IsExpanded = true;   // Expands all parents
				if (!wasExpanded)
					tk.IsExpanded = false;   // Collapses the item again like it was before
			}
			if (async)
				ViewCommandManager.InvokeLoaded("SelectTextKey", tk);
			else
				ViewCommandManager.Invoke("SelectTextKey", tk);
		}

		private void SelectCultureText(TextKeyViewModel tk, string cultureName)
		{
			if (tk != null &&
				tk.CultureTextVMs != null)
			{
				var ct = tk.CultureTextVMs.FirstOrDefault(vm => vm.CultureName == cultureName);
				if (ct != null)
				{
					ct.ViewCommandManager.InvokeLoaded("FocusText");
				}
			}
		}

		private void ClearViewHistory()
		{
			viewHistory.Clear();
			if (selectedTextKeys != null && selectedTextKeys.Count > 0)
				viewHistory.Add(selectedTextKeys[0]);
			else
				viewHistory.Add(null);
			viewHistoryIndex = 0;
		}

		private void AppendViewHistory()
		{
			if (navigatingHistory.IsSet)
			{
				// Currently navigating through the history, don't interfer that
				return;
			}
			if (selectedTextKeys != null &&
				selectedTextKeys.Count > 0 &&
				selectedTextKeys[0] == viewHistory[viewHistory.Count - 1])
			{
				// First selected item has not changed, do nothing
				return;
			}
			if (selectedTextKeys != null &&
				selectedTextKeys.Count == 0)
			{
				// Nothing selected, nothing to remember
				return;
			}

			// Clear any future history
			while (viewHistory.Count > viewHistoryIndex + 1)
			{
				viewHistory.RemoveAt(viewHistory.Count - 1);
			}

			if (selectedTextKeys != null && selectedTextKeys.Count > 0)
				viewHistory.Add(selectedTextKeys[0]);
			else
				viewHistory.Add(null);
			viewHistoryIndex++;
		}

		private void ViewHistoryBack()
		{
			if (viewHistoryIndex > 0)
			{
				using (new OpLock(navigatingHistory))
				{
					viewHistoryIndex--;
					SelectTextKey(viewHistory[viewHistoryIndex]);
					SelectCultureText(viewHistory[viewHistoryIndex], LastSelectedCulture);
				}
			}
		}

		private void ViewHistoryForward()
		{
			if (viewHistoryIndex < viewHistory.Count - 1)
			{
				using (new OpLock(navigatingHistory))
				{
					viewHistoryIndex++;
					SelectTextKey(viewHistory[viewHistoryIndex]);
					SelectCultureText(viewHistory[viewHistoryIndex], LastSelectedCulture);
				}
			}
		}

		private DelegateCommand initCommand;
		public DelegateCommand InitCommand
		{
			get
			{
				if (initCommand == null)
				{
					initCommand = new DelegateCommand(OnInit);
				}
				return initCommand;
			}
		}

		private void OnInit()
		{
			if (App.SplashScreen != null)
			{
				App.SplashScreen.Close(TimeSpan.FromMilliseconds(300));
				// Work-around for implementation bug in SplashScreen.Close that steals the focus
				MainWindow.Instance.Focus();
			}

			if (!string.IsNullOrWhiteSpace(ScanDirectory))
			{
				var sfVM = new SelectFileViewModel(ScanDirectory);
			    var sfw = new SelectFileWindow
			    {
			        WindowStartupLocation = WindowStartupLocation.CenterOwner,
			        Owner = MainWindow.Instance,
			        DataContext = sfVM
			    };
			    if (sfw.ShowDialog() == true)
				{
				    Load(sfVM.SelectedFileNames.Select(f => new FileLocation(f)).Cast<ISerializeLocation>().ToArray());
				}
			}
		}

		#endregion Window management

		#region Text search

		private DelayedCall searchDc;
		private string searchText = "";   // Initialise so that it's not changed at startup
		public string SearchText
		{
			get
			{
				return searchText;
			}
			set
			{
				if (value != searchText)
				{
					searchText = value;
					OnPropertyChanged("SearchText");
					searchDc.Reset();
				}
			}
		}

		private string shadowSearchText;
		public string ShadowSearchText
		{
			get
			{
				return shadowSearchText;
			}
			set
			{
				if (value != shadowSearchText)
				{
					shadowSearchText = value;
					OnPropertyChanged("ShadowSearchText");
				}
			}
		}

		/// <summary>
		/// Updates the visibility of all text keys in the tree, according to the entered search term.
		/// </summary>
		public void UpdateSearch()
		{
			ShadowSearchText = SearchText;

			var isSearch = !string.IsNullOrWhiteSpace(searchText);
			var count = RootKeys.Aggregate(0, (i, root) => UpdateTextKeyVisibility(root, isSearch));
            StatusText = isSearch ? Tx.T("statusbar.n results", count) : "";
		}

		private int UpdateTextKeyVisibility(TextKeyViewModel tk, bool isSearch)
		{
			int count = 0;
			foreach (TextKeyViewModel child in tk.Children.Enumerate<TextKeyViewModel>())
			{
				bool isVisible =
					!isSearch ||
					child.TextKey.ToLower().Contains(searchText.ToLower()) ||
					child.CultureTextVMs.Any(ct => ct.Text != null && ct.Text.ToLower().Contains(searchText.ToLower()));
				if (ProblemFilterActive)
				{
					isVisible &= child.HasOwnProblem || child.HasProblem;
				}

				child.IsVisible = isVisible;
				if (isVisible)
				{
					count++;
					TreeViewItemViewModel parent = child.Parent;
					while (parent != null)
					{
						parent.IsVisible = true;
						parent = parent.Parent;
					}
				}
				if (child.Children.Count > 0)
				{
					count += UpdateTextKeyVisibility(child, isSearch);
				}
			}
			return count;
		}

		#endregion Text search

		#region Suggestions

		private void UpdateSuggestionsLayout()
		{
			if (App.Settings.View.ShowSuggestions)
			{
				if (App.Settings.View.SuggestionsHorizontalLayout)
				{
					SuggestionsSplitterHeight = 0;
					SuggestionsPanelHeight = 0;
					SuggestionsSplitterWidth = 6;
					SuggestionsPanelWidth = App.Settings.View.SuggestionsWidth;
				}
				else
				{
					SuggestionsSplitterHeight = 6;
					SuggestionsPanelHeight = App.Settings.View.SuggestionsHeight;
					SuggestionsSplitterWidth = 0;
					SuggestionsPanelWidth = 0;
				}
			}
			else
			{
				SuggestionsSplitterHeight = 0;
				SuggestionsPanelHeight = 0;
				SuggestionsSplitterWidth = 0;
				SuggestionsPanelWidth = 0;
			}
		}

		private void AddDummySuggestion()
		{
			SuggestionViewModel suggestion = new SuggestionViewModel(this);
			suggestion.IsDummy = true;
			suggestion.BaseText = Tx.T("suggestions.none");
			suggestions.Add(suggestion);
		}

		private void UpdateSuggestionsLater()
		{
			TaskHelper.Background(UpdateSuggestions);
		}

		private void UpdateSuggestions()
		{
			Match m;

			suggestions.Clear();
			HaveSuggestions = false;

			if (string.IsNullOrEmpty(LastSelectedCulture))
			{
				AddDummySuggestion();
				return;
			}
			if (!LoadedCultureNames.Contains(LastSelectedCulture))
			{
				AddDummySuggestion();
				return;
			}
			SuggestionsCulture = CultureInfoName(new CultureInfo(LastSelectedCulture), false);
			//if (lastSelectedCulture == primaryCulture) return;

			TextKeyViewModel tk = selectedTextKeys != null && selectedTextKeys.Count > 0 ? selectedTextKeys[0] : null;
			if (tk == null || tk.CultureTextVMs.Count < 1)
			{
				AddDummySuggestion();
				return;
			}

			// The text we're finding translation suggestions for
			string refText = tk.CultureTextVMs[0].Text;
			string origRefText = refText;
			if (refText == null)
			{
				AddDummySuggestion();
				return;
			}

			//// Find the most common words to filter them out
			//Dictionary<string, int> wordCount = new Dictionary<string, int>();
			//foreach (var kvp in TextKeys)
			//{
			//    string otherBaseText = kvp.Value.CultureTextVMs[0].Text;
			//    if (string.IsNullOrEmpty(otherBaseText)) continue;

			//    // Remove all placeholders and key references
			//    string otherText = Regex.Replace(otherBaseText, @"(?<!\{)\{[^{]*?\}", "");

			//    // Extract all words
			//    m = Regex.Match(otherText, @"(\w{2,})");
			//    while (m.Success)
			//    {
			//        string lcWord = m.Groups[1].Value.ToLowerInvariant();

			//        int count;
			//        if (wordCount.TryGetValue(lcWord, out count))
			//        {
			//            wordCount[lcWord] = count + 1;
			//        }
			//        else
			//        {
			//            wordCount[lcWord] = 1;
			//        }

			//        m = m.NextMatch();
			//    }
			//}

			//HashSet<string> commonWords = new HashSet<string>();
			//if (wordCount.Count > 0)
			//{
			//    int maxCount = wordCount.Select(kvp => kvp.Value).Max();
			//    foreach (var kvp in wordCount.OrderByDescending(kvp => kvp.Value))
			//    {
			//        if (commonWords.Count < (int) (wordCount.Count * 0.05) ||
			//            kvp.Value >= (int) (maxCount * 0.8))
			//        {
			//            commonWords.Add(kvp.Key);
			//        }
			//    }
			//}

			//commonWords.Clear();
			//commonWords.Add("all");
			//commonWords.Add("also");
			//commonWords.Add("an");
			//commonWords.Add("and");
			//commonWords.Add("are");
			//commonWords.Add("as");
			//commonWords.Add("at");
			//commonWords.Add("be");
			//commonWords.Add("but");
			//commonWords.Add("by");
			//commonWords.Add("can");
			//commonWords.Add("cannot");
			//commonWords.Add("do");
			//commonWords.Add("don");
			//commonWords.Add("each");
			//commonWords.Add("for");
			//commonWords.Add("from");
			//commonWords.Add("have");
			//commonWords.Add("if");
			//commonWords.Add("in");
			//commonWords.Add("into");
			//commonWords.Add("is");
			//commonWords.Add("it");
			//commonWords.Add("its");
			//commonWords.Add("may");
			//commonWords.Add("must");
			//commonWords.Add("no");
			//commonWords.Add("not");
			//commonWords.Add("of");
			//commonWords.Add("on");
			//commonWords.Add("please");
			//commonWords.Add("that");
			//commonWords.Add("the");
			//commonWords.Add("there");
			//commonWords.Add("this");
			//commonWords.Add("those");
			//commonWords.Add("to");
			//commonWords.Add("were");
			//commonWords.Add("will");
			//commonWords.Add("with");
			//commonWords.Add("would");
			//commonWords.Add("you");
			//commonWords.Add("your");

			HashSet<string> commonWords;
			if (LastSelectedCulture.StartsWith("de"))
			{
				// GERMAN STOPWORDS
				// Zusammmengetragen von Marco Götze, Steffen Geyer
				// Last update: 2011-01-15
				// Source: http://solariz.de/649/deutsche-stopwords.htm
				// Via: http://en.wikipedia.org/wiki/Stop_words
				commonWords = new HashSet<string>(new string[]
				{
					"ab", "aber", "abgerufen", "abgerufene", "abgerufener", "abgerufenes", "acht", "ähnlich", "alle", "allein", "allem",
					"allen", "aller", "allerdings", "allerlei", "alles", "allgemein", "allmählich", "allzu", "als", "alsbald", "also",
					"am", "an", "ander", "andere", "anderem", "anderen", "anderer", "andererseits", "anderes", "anderm", "andern",
					"andernfalls", "anders", "anerkannt", "anerkannte", "anerkannter", "anerkanntes", "anfangen", "anfing", "angefangen",
					"angesetze", "angesetzt", "angesetzten", "angesetzter", "ansetzen", "anstatt", "arbeiten", "auch", "auf", "aufgehört",
					"aufgrund", "aufhören", "aufhörte", "aufzusuchen", "aus", "ausdrücken", "ausdrückt", "ausdrückte", "ausgenommen",
					"außen", "ausser", "außer", "ausserdem", "außerdem", "außerhalb", "author", "autor", "bald", "bearbeite",
					"bearbeiten", "bearbeitete", "bearbeiteten", "bedarf", "bedürfen", "bedurfte", "befragen", "befragte", "befragten",
					"befragter", "begann", "beginnen", "begonnen", "behalten", "behielt", "bei", "beide", "beiden", "beiderlei", "beides",
					"beim", "beinahe", "beitragen", "beitrugen", "bekannt", "bekannte", "bekannter", "bekennen", "benutzt", "bereits",
					"berichten", "berichtet", "berichtete", "berichteten", "besonders", "besser", "bestehen", "besteht", "beträchtlich",
					"bevor", "bezüglich", "bietet", "bin", "bis", "bis", "bisher", "bislang", "bist", "bleiben", "blieb", "bloß", "bloss",
					"böden", "brachte", "brachten", "brauchen", "braucht", "bräuchte", "bringen", "bsp", "bzw", "ca", "da", "dabei",
					"dadurch", "dafür", "dagegen", "daher", "dahin", "damals", "damit", "danach", "daneben", "dank", "danke", "danken",
					"dann", "dannen", "daran", "darauf", "daraus", "darf", "darfst", "darin", "darüber", "darüberhinaus", "darum",
					"darunter", "das", "daß", "dass", "dasselbe", "davon", "davor", "dazu", "dein", "deine", "deinem", "deinen", "deiner",
					"deines", "dem", "demnach", "demselben", "den", "denen", "denn", "dennoch", "denselben", "der", "derart", "derartig",
					"derem", "deren", "derer", "derjenige", "derjenigen", "derselbe", "derselben", "derzeit", "des", "deshalb",
					"desselben", "dessen", "desto", "deswegen", "dich", "die", "diejenige", "dies", "diese", "dieselbe", "dieselben",
					"diesem", "diesen", "dieser", "dieses", "diesseits", "dinge", "dir", "direkt", "direkte", "direkten", "direkter",
					"doch", "doppelt", "dort", "dorther", "dorthin", "drauf", "drei", "dreißig", "drin", "dritte", "drüber", "drunter",
					"du", "dunklen", "durch", "durchaus", "dürfen", "durfte", "dürfte", "durften", "eben", "ebenfalls", "ebenso", "ehe",
					"eher", "eigenen", "eigenes", "eigentlich", "ein", "einbaün", "eine", "einem", "einen", "einer", "einerseits",
					"eines", "einfach", "einführen", "einführte", "einführten", "eingesetzt", "einig", "einige", "einigem", "einigen",
					"einiger", "einigermaßen", "einiges", "einmal", "eins", "einseitig", "einseitige", "einseitigen", "einseitiger",
					"einst", "einstmals", "einzig", "ende", "entsprechend", "entweder", "er", "ergänze", "ergänzen", "ergänzte",
					"ergänzten", "erhält", "erhalten", "erhielt", "erhielten", "erneut", "eröffne", "eröffnen", "eröffnet", "eröffnete",
					"eröffnetes", "erst", "erste", "ersten", "erster", "es", "etc", "etliche", "etwa", "etwas", "euch", "euer", "eure",
					"eurem", "euren", "eurer", "eures", "fall", "falls", "fand", "fast", "ferner", "finden", "findest", "findet",
					"folgende", "folgenden", "folgender", "folgendes", "folglich", "fordern", "fordert", "forderte", "forderten",
					"fortsetzen", "fortsetzt", "fortsetzte", "fortsetzten", "fragte", "frau", "frei", "freie", "freier", "freies", "fuer",
					"fünf", "für", "gab", "gängig", "gängige", "gängigen", "gängiger", "gängiges", "ganz", "ganze", "ganzem", "ganzen",
					"ganzer", "ganzes", "gänzlich", "gar", "gbr", "geb", "geben", "geblieben", "gebracht", "gedurft", "geehrt", "geehrte",
					"geehrten", "geehrter", "gefallen", "gefälligst", "gefällt", "gefiel", "gegeben", "gegen", "gehabt", "gehen", "geht",
					"gekommen", "gekonnt", "gemacht", "gemäss", "gemocht", "genommen", "genug", "gern", "gesagt", "gesehen", "gestern",
					"gestrige", "getan", "geteilt", "geteilte", "getragen", "gewesen", "gewissermaßen", "gewollt", "geworden", "ggf",
					"gib", "gibt", "gleich", "gleichwohl", "gleichzeitig", "glücklicherweise", "gmbh", "gratulieren", "gratuliert",
					"gratulierte", "gute", "guten", "hab", "habe", "haben", "haette", "halb", "hallo", "hast", "hat", "hätt", "hatte",
					"hätte", "hatten", "hätten", "hattest", "hattet", "heraus", "herein", "heute", "heutige", "hier", "hiermit",
					"hiesige", "hin", "hinein", "hinten", "hinter", "hinterher", "hoch", "höchstens", "hundert", "ich", "igitt", "ihm",
					"ihn", "ihnen", "ihr", "ihre", "ihrem", "ihren", "ihrer", "ihres", "im", "immer", "immerhin", "important", "in",
					"indem", "indessen", "info", "infolge", "innen", "innerhalb", "ins", "insofern", "inzwischen", "irgend", "irgendeine",
					"irgendwas", "irgendwen", "irgendwer", "irgendwie", "irgendwo", "ist", "ja", "jährig", "jährige", "jährigen",
					"jähriges", "je", "jede", "jedem", "jeden", "jedenfalls", "jeder", "jederlei", "jedes", "jedoch", "jemand", "jene",
					"jenem", "jenen", "jener", "jenes", "jenseits", "jetzt", "kam", "kann", "kannst", "kaum", "kein", "keine", "keinem",
					"keinen", "keiner", "keinerlei", "keines", "keines", "keineswegs", "klar", "klare", "klaren", "klares", "klein",
					"kleinen", "kleiner", "kleines", "koennen", "koennt", "koennte", "koennten", "komme", "kommen", "kommt", "konkret",
					"konkrete", "konkreten", "konkreter", "konkretes", "könn", "können", "könnt", "konnte", "könnte", "konnten",
					"könnten", "künftig", "lag", "lagen", "langsam", "längst", "längstens", "lassen", "laut", "lediglich", "leer",
					"legen", "legte", "legten", "leicht", "leider", "lesen", "letze", "letzten", "letztendlich", "letztens", "letztes",
					"letztlich", "lichten", "liegt", "liest", "links", "mache", "machen", "machst", "macht", "machte", "machten", "mag",
					"magst", "mal", "man", "manche", "manchem", "manchen", "mancher", "mancherorts", "manches", "manchmal", "mann",
					"margin", "mehr", "mehrere", "mein", "meine", "meinem", "meinen", "meiner", "meines", "meist", "meiste", "meisten",
					"meta", "mich", "mindestens", "mir", "mit", "mithin", "mochte", "möchte", "möchten", "möchtest", "mögen", "möglich",
					"mögliche", "möglichen", "möglicher", "möglicherweise", "morgen", "morgige", "muessen", "muesst", "muesste", "muss",
					"muß", "müssen", "mußt", "musst", "müßt", "musste", "müßte", "müsste", "mussten", "müssten", "nach", "nachdem",
					"nacher", "nachhinein", "nächste", "nacht", "nahm", "nämlich", "natürlich", "neben", "nebenan", "nehmen", "nein",
					"neu", "neue", "neuem", "neuen", "neuer", "neues", "neun", "nicht", "nichts", "nie", "niemals", "niemand", "nimm",
					"nimmer", "nimmt", "nirgends", "nirgendwo", "noch", "nötigenfalls", "nun", "nur", "nutzen", "nutzt", "nützt",
					"nutzung", "ob", "oben", "oberhalb", "obgleich", "obschon", "obwohl", "oder", "oft", "ohne", "per", "pfui",
					"plötzlich", "pro", "reagiere", "reagieren", "reagiert", "reagierte", "rechts", "regelmäßig", "rief", "rund", "sage",
					"sagen", "sagt", "sagte", "sagten", "sagtest", "sämtliche", "sang", "sangen", "schätzen", "schätzt", "schätzte",
					"schätzten", "schlechter", "schließlich", "schnell", "schon", "schreibe", "schreiben", "schreibens", "schreiber",
					"schwierig", "sechs", "sect", "sehe", "sehen", "sehr", "sehrwohl", "seht", "sei", "seid", "sein", "seine", "seinem",
					"seinen", "seiner", "seines", "seit", "seitdem", "seite", "seiten", "seither", "selber", "selbst", "senke", "senken",
					"senkt", "senkte", "senkten", "setzen", "setzt", "setzte", "setzten", "sich", "sicher", "sicherlich", "sie", "sieben",
					"siebte", "siehe", "sieht", "sind", "singen", "singt", "so", "sobald", "sodaß", "soeben", "sofern", "sofort", "sog",
					"sogar", "solange", "solc hen", "solch", "solche", "solchem", "solchen", "solcher", "solches", "soll", "sollen",
					"sollst", "sollt", "sollte", "sollten", "solltest", "somit", "sondern", "sonst", "sonstwo", "sooft", "soviel",
					"soweit", "sowie", "sowohl", "später", "spielen", "startet", "startete", "starteten", "statt", "stattdessen", "steht",
					"steige", "steigen", "steigt", "stets", "stieg", "stiegen", "such", "suchen", "tages", "tat", "tät", "tatsächlich",
					"tatsächlichen", "tatsächlicher", "tatsächliches", "tausend", "teile", "teilen", "teilte", "teilten", "titel",
					"total", "trage", "tragen", "trägt", "trotzdem", "trug", "tun", "tust", "tut", "txt", "übel", "über", "überall",
					"überallhin", "überdies", "übermorgen", "übrig", "übrigens", "ueber", "um", "umso", "unbedingt", "und", "ungefähr",
					"unmöglich", "unmögliche", "unmöglichen", "unmöglicher", "unnötig", "uns", "unse", "unsem", "unsen", "unser", "unser",
					"unsere", "unserem", "unseren", "unserer", "unseres", "unserm", "unses", "unten", "unter", "unterbrach",
					"unterbrechen", "unterhalb", "unwichtig", "usw", "vergangen", "vergangene", "vergangener", "vergangenes", "vermag",
					"vermögen", "vermutlich", "veröffentlichen", "veröffentlicher", "veröffentlicht", "veröffentlichte",
					"veröffentlichten", "veröffentlichtes", "verrate", "verraten", "verriet", "verrieten", "version", "versorge",
					"versorgen", "versorgt", "versorgte", "versorgten", "versorgtes", "viel", "viele", "vielen", "vieler", "vieles",
					"vielleicht", "vielmals", "vier", "völlig", "vollständig", "vom", "von", "vor", "voran", "vorbei", "vorgestern",
					"vorher", "vorne", "vorüber", "wachen", "waere", "während", "während", "währenddessen", "wann", "war", "wär", "wäre",
					"waren", "wären", "warst", "warum", "was", "weder", "weg", "wegen", "weil", "weiß", "weiter", "weitere", "weiterem",
					"weiteren", "weiterer", "weiteres", "weiterhin", "welche", "welchem", "welchen", "welcher", "welches", "wem", "wen",
					"wenig", "wenige", "weniger", "wenigstens", "wenn", "wenngleich", "wer", "werde", "werden", "werdet", "weshalb",
					"wessen", "wichtig", "wie", "wieder", "wieso", "wieviel", "wiewohl", "will", "willst", "wir", "wird", "wirklich",
					"wirst", "wo", "wodurch", "wogegen", "woher", "wohin", "wohingegen", "wohl", "wohlweislich", "wolle", "wollen",
					"wollt", "wollte", "wollten", "wolltest", "wolltet", "womit", "woraufhin", "woraus", "worin", "wurde", "würde",
					"wurden", "würden", "zahlreich", "zehn", "zeitweise", "ziehen", "zieht", "zog", "zogen", "zu", "zudem", "zuerst",
					"zufolge", "zugleich", "zuletzt", "zum", "zumal", "zur", "zurück", "zusammen", "zuviel", "zwanzig", "zwar", "zwei",
					"zwischen", "zwölf"
				});
			}
			else if (LastSelectedCulture.StartsWith("en"))
			{
				// English stop words
				// Source: http://norm.al/2009/04/14/list-of-english-stop-words/ (MySQL fulltext, from 2009-10-03)
				// Via: http://en.wikipedia.org/wiki/Stop_words
				commonWords = new HashSet<string>(new string[]
				{
					"able", "about", "above", "according", "accordingly", "across", "actually", "after", "afterwards", "again", "against",
					"ain", "all", "allow", "allows", "almost", "alone", "along", "already", "also", "although", "always", "am", "among",
					"amongst", "an", "and", "another", "any", "anybody", "anyhow", "anyone", "anything", "anyway", "anyways", "anywhere",
					"apart", "appear", "appreciate", "appropriate", "are", "aren", "around", "as", "aside", "ask", "asking", "associated",
					"at", "available", "away", "awfully", "be", "became", "because", "become", "becomes", "becoming", "been", "before",
					"beforehand", "behind", "being", "believe", "below", "beside", "besides", "best", "better", "between", "beyond",
					"both", "brief", "but", "by", "mon", "came", "can", "cannot", "cause", "causes", "certain", "certainly", "changes",
					"clearly", "co", "com", "come", "comes", "concerning", "consequently", "consider", "considering", "contain",
					"containing", "contains", "corresponding", "could", "couldn", "course", "currently", "definitely", "described",
					"despite", "did", "didn", "different", "do", "does", "doesn", "doing", "don", "done", "down", "downwards", "during",
					"each", "edu", "eg", "eight", "either", "else", "elsewhere", "enough", "entirely", "especially", "et", "etc", "even",
					"ever", "every", "everybody", "everyone", "everything", "everywhere", "ex", "exactly", "example", "except", "far",
					"few", "fifth", "first", "five", "followed", "following", "follows", "for", "former", "formerly", "forth", "four",
					"from", "further", "furthermore", "get", "gets", "getting", "given", "gives", "go", "goes", "going", "gone", "got",
					"gotten", "greetings", "had", "hadn", "happens", "hardly", "has", "hasn", "have", "haven", "having", "he", "hello",
					"help", "hence", "her", "here", "hereafter", "hereby", "herein", "hereupon", "hers", "herself", "hi", "him",
					"himself", "his", "hither", "hopefully", "how", "howbeit", "however", "if", "ignored", "immediate", "in", "inasmuch",
					"inc", "indeed", "indicate", "indicated", "indicates", "inner", "insofar", "instead", "into", "inward", "is", "isn",
					"it", "its", "itself", "just", "keep", "keeps", "kept", "know", "knows", "known", "last", "lately", "later", "latter",
					"latterly", "least", "less", "lest", "let", "like", "liked", "likely", "little", "ll", "look", "looking", "looks",
					"ltd", "mainly", "many", "may", "maybe", "me", "mean", "meanwhile", "merely", "might", "more", "moreover", "most",
					"mostly", "much", "must", "my", "myself", "name", "namely", "nd", "near", "nearly", "necessary", "need", "needs",
					"neither", "never", "nevertheless", "new", "next", "nine", "no", "nobody", "non", "none", "noone", "nor", "normally",
					"not", "nothing", "novel", "now", "nowhere", "obviously", "of", "off", "often", "oh", "ok", "okay", "old", "on",
					"once", "one", "ones", "only", "onto", "or", "other", "others", "otherwise", "ought", "our", "ours", "ourselves",
					"out", "outside", "over", "overall", "own", "particular", "particularly", "per", "perhaps", "placed", "please",
					"plus", "possible", "presumably", "probably", "provides", "que", "quite", "qv", "rather", "rd", "re", "really",
					"reasonably", "regarding", "regardless", "regards", "relatively", "respectively", "right", "said", "same", "saw",
					"say", "saying", "says", "second", "secondly", "see", "seeing", "seem", "seemed", "seeming", "seems", "seen", "self",
					"selves", "sensible", "sent", "serious", "seriously", "seven", "several", "shall", "she", "should", "shouldn",
					"since", "six", "so", "some", "somebody", "somehow", "someone", "something", "sometime", "sometimes", "somewhat",
					"somewhere", "soon", "sorry", "specified", "specify", "specifying", "still", "sub", "such", "sup", "sure", "take",
					"taken", "tell", "tends", "th", "than", "thank", "thanks", "thanx", "that", "thats", "the", "their", "theirs", "them",
					"themselves", "then", "thence", "there", "thereafter", "thereby", "therefore", "therein", "theres", "thereupon",
					"these", "they", "think", "third", "this", "thorough", "thoroughly", "those", "though", "three", "through",
					"throughout", "thru", "thus", "to", "together", "too", "took", "toward", "towards", "tried", "tries", "truly", "try",
					"trying", "twice", "two", "un", "under", "unfortunately", "unless", "unlikely", "until", "unto", "up", "upon", "us",
					"use", "used", "useful", "uses", "using", "usually", "value", "various", "ve", "very", "via", "viz", "vs", "want",
					"wants", "was", "wasn", "way", "we", "welcome", "well", "went", "were", "weren", "what", "whatever", "when", "whence",
					"whenever", "where", "whereafter", "whereas", "whereby", "wherein", "whereupon", "wherever", "whether", "which",
					"while", "whither", "who", "whoever", "whole", "whom", "whose", "why", "will", "willing", "wish", "with", "within",
					"without", "won", "wonder", "would", "would", "wouldn", "yes", "yet", "you", "your", "yours", "yourself",
					"yourselves", "zero"
				});
			}
			else
			{
				commonWords = new HashSet<string>();
			}

			// Remove all placeholders and key references
			refText = Regex.Replace(refText, @"(?<!\{)\{[^{]*?\}", "");

			// Extract all words
			List<string> refWords = new List<string>();
			m = Regex.Match(refText, @"(\w{2,})");
			while (m.Success)
			{
				if (!commonWords.Contains(m.Groups[1].Value.ToLowerInvariant()))   // Skip common words
					refWords.Add(m.Groups[1].Value);
				m = m.NextMatch();
			}

			// Find other text keys that contain these words in their primary culture text
			Dictionary<TextKeyViewModel, float> otherKeys = new Dictionary<TextKeyViewModel, float>();
			foreach (var kvp in TextKeys)
			{
				float score = 0;
				bool isExactMatch = false;
			    foreach (var vm in kvp.Value)
			    {
                    if (vm.TextKey == tk.TextKey) continue;   // Skip currently selected item
                    if (vm.TextKey.StartsWith("Tx:")) continue;   // Skip system keys

                    string otherBaseText = vm.CultureTextVMs[0].Text;
			        string otherTranslatedText = vm.CultureTextVMs.First(ct => ct.CultureName == LastSelectedCulture).Text;

			        if (string.IsNullOrEmpty(otherBaseText)) continue;
			        if (string.IsNullOrEmpty(otherTranslatedText)) continue;

			        if (otherBaseText == origRefText)
			        {
			            // Both keys' primary translation matches exactly
			            isExactMatch = true;
			        }

			        // Remove all placeholders and key references
			        string otherText = Regex.Replace(otherBaseText, @"(?<!\{)\{[^{]*?\}", "");

			        // Extract all words
			        List<string> otherWords = new List<string>();
			        m = Regex.Match(otherText, @"(\w{2,})");
			        while (m.Success)
			        {
			            if (!commonWords.Contains(m.Groups[1].Value.ToLowerInvariant()))   // Skip common words
			                otherWords.Add(m.Groups[1].Value);
			            m = m.NextMatch();
			        }

			        // Increase score by 1 for each case-insensitively matching word
			        foreach (string word in refWords)
			        {
			            if (otherWords.Any(w => string.Equals(w, word, StringComparison.InvariantCultureIgnoreCase)))
			                score += 1;
			        }
			        // Increase score by 2 for each case-sensitively matching word
			        foreach (string word in refWords)
			        {
			            if (otherWords.Any(w => string.Equals(w, word, StringComparison.InvariantCulture)))
			                score += 2;
			        }

			        // Divide by the square root of the number of relevant words. (Using the square
			        // root to reduce the effect for very long texts.)
			        if (otherWords.Count > 0)
			        {
			            score /= (float)Math.Sqrt(otherWords.Count);
			        }
			        else
			        {
			            // There are no significant words in the other text
			            score = 0;
			        }

			        if (isExactMatch)
			        {
			            score = 100000;
			        }

			        // Accept every text key with a threshold score
			        if (score >= 0.5f)
			        {
			            otherKeys.Add(vm, score);
			        }
			    }
			}

			// Sort all matches by their score
			foreach (var kvp in otherKeys.OrderByDescending(kvp => kvp.Value))
			{
				try
				{
					SuggestionViewModel suggestion = new SuggestionViewModel(this);
					suggestion.TextKey = kvp.Key.TextKey;
					suggestion.BaseText = kvp.Key.CultureTextVMs[0].Text;
					if (LastSelectedCulture != PrimaryCulture)
						suggestion.TranslatedText = kvp.Key.CultureTextVMs.First(ct => ct.CultureName == LastSelectedCulture).Text;
					suggestion.IsExactMatch = kvp.Value >= 100000;
					suggestion.ScoreNum = kvp.Value;
					if (suggestion.IsExactMatch)
						suggestion.Score = Tx.T("suggestions.exact match");
					else
						suggestion.Score = kvp.Value.ToString("0.00");
					suggestions.Add(suggestion);
				}
				catch
				{
					// Something's missing (probably a LINQ-related exception), ignore that item
				}
			}

			if (suggestions.Count == 0)
			{
				AddDummySuggestion();
			}
			else
			{
				HaveSuggestions = true;
			}
		}

        #endregion Suggestions


        #region Public methods

        public void ModelWasChanged(RootKeyViewModel rootKeyViewModel)
        {
            if (!dateTimeWindow.IsClosed()) dateTimeWindow.UpdateView();

            UpdateTitle();
            SaveCommand.RaiseCanExecuteChanged();
        }

        public RootKeyViewModel GetSelectedRoot()
        {
            var key = MainWindow.Instance.TextKeysTreeView.LastSelectedItem as TreeViewItemViewModel;
            return key.FindRoot();
        }

        #endregion

        #region IViewCommandSource members

        private ViewCommandManager viewCommandManager = new ViewCommandManager();
		public ViewCommandManager ViewCommandManager { get { return viewCommandManager; } }

		#endregion IViewCommandSource members

	    class CreateNewTranslationObject
	    {
	        public string DisplayName
	        {
	            get { return string.Format("New translation"); }
	        } 
	    }
	}
}
