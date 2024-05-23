using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Input;
using AvaloniaEdit;
using AvaloniaEdit.TextMate;
using TextMateSharp.Grammars;

namespace PolycodeCompiler
{
    public partial class MainWindow : Window
    {
        private string _currentFilePath = null;
        private string _selectedDirectory = null;
        private RegistryOptions _registryOptions;
        private TextMate.Installation _textMateInstallation;
        private bool _isTextChanged = false;
        private string _defaultDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PolycodeProjects");

        public MainWindow()
        {
            InitializeComponent();
            AddKeyBindings();
            InitializeTextMate();
            LanguageSelector.SelectionChanged += LanguageSelector_SelectionChanged;
            CodeInput.TextChanged += CodeInput_TextChanged;
        }

        private void LoadFileTree()
        {
            if (string.IsNullOrEmpty(_selectedDirectory))
            {
                return;
            }

            if (!Directory.Exists(_selectedDirectory))
            {
                Directory.CreateDirectory(_selectedDirectory);
            }

            FileTreeView.Items.Clear();
            var rootDirectoryName = new DirectoryInfo(_selectedDirectory).Name;
            var root = new TreeViewItem { Header = rootDirectoryName, IsExpanded = true };
            LoadDirectory(_selectedDirectory, root);
            FileTreeView.Items.Add(root);
        }

        private void LoadDirectory(string directory, TreeViewItem parent)
        {
            foreach (var dir in Directory.GetDirectories(directory))
            {
                var dirItem = new TreeViewItem { Header = Path.GetFileName(dir) };
                LoadDirectory(dir, dirItem);
                parent.Items.Add(dirItem);
            }

            foreach (var file in Directory.GetFiles(directory))
            {
                var fileItem = new TreeViewItem { Header = Path.GetFileName(file) };
                fileItem.PointerPressed += (sender, e) => OpenFile(Path.Combine(directory, (sender as TreeViewItem).Header.ToString()));
                parent.Items.Add(fileItem);
            }
        }

        private async void OnSelectFolderClick(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFolderDialog();
            var result = await dialog.ShowAsync(this);
            if (result != null)
            {
                _selectedDirectory = result;
                LoadFileTree();
            }
        }

        private void OpenFile(string filePath)
        {
            _currentFilePath = filePath;
            CodeInput.Text = File.ReadAllText(_currentFilePath);
            _isTextChanged = false;
            UpdateTitle();
        }

        private async void OnSaveFile(object sender, RoutedEventArgs e)
        {
            if (_currentFilePath != null)
            {
                File.WriteAllText(_currentFilePath, CodeInput.Text);
                _isTextChanged = false;
                UpdateTitle();
            }
            else
            {
                if (string.IsNullOrEmpty(_selectedDirectory))
                {
                    _selectedDirectory = _defaultDirectory;
                    if (!Directory.Exists(_defaultDirectory))
                    {
                        Directory.CreateDirectory(_defaultDirectory);
                    }
                }

                await SaveFileAs();
            }
        }

        private async void OnSaveFileAs(object sender, RoutedEventArgs e)
        {
            await SaveFileAs();
        }

        private async Task SaveFileAs()
        {
            var saveDialog = new SaveFileDialog();
            saveDialog.Filters.Add(new FileDialogFilter { Name = "Все файлы", Extensions = { "*" } });

            string defaultFileName = GetDefaultFileNameByLanguage();
            saveDialog.InitialFileName = defaultFileName;

            var result = await saveDialog.ShowAsync(this);
            if (result != null)
            {
                var filePath = Path.Combine(_selectedDirectory, Path.GetFileName(result));
                File.WriteAllText(filePath, CodeInput.Text);
                _currentFilePath = filePath;
                _isTextChanged = false;
                LoadFileTree();
                UpdateTitle();
            }
        }

        private string GetDefaultFileNameByLanguage()
        {
            var selectedItem = LanguageSelector.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.IsEnabled)
            {
                var language = selectedItem.Content.ToString();
                switch (language)
                {
                    case "C#":
                        return "new_file.cs";
                    case "C++":
                        return "new_file.cpp";
                    case "Python":
                        return "new_file.py";
                    case "JavaScript":
                        return "new_file.js";
                    case "Java":
                        return "new_file.java";
                    case "C":
                        return "new_file.c";
                    case "Go":
                        return "new_file.go";
                    case "Pascal":
                        return "new_file.pas";
                    case "PHP":
                        return "new_file.php";
                    case "Ruby":
                        return "new_file.rb";
                    case "TypeScript":
                        return "new_file.ts";
                    default:
                        return "new_file.txt";
                }
            }
            return "new_file.txt";
        }

        private void OnCompileRun(object sender, RoutedEventArgs e)
        {
            CompileAndRun();
        }

        private void CodeInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Tab)
            {
                e.Handled = true;

                var textBox = sender as TextBox;
                if (textBox != null)
                {
                    int caretIndex = textBox.CaretIndex;

                    if (textBox.Text == null)
                    {
                        textBox.Text = "";
                    }

                    textBox.Text = textBox.Text.Insert(caretIndex, "\t");
                    textBox.CaretIndex = caretIndex + 1;
                }
            }
        }

        private void AddKeyBindings()
        {
            this.AddHandler(KeyDownEvent, (sender, e) =>
            {
                if (e.Key == Key.S && (e.KeyModifiers & KeyModifiers.Control) != 0)
                {
                    OnSaveFile(sender, null);
                    e.Handled = true;
                }
                else if (e.Key == Key.F5)
                {
                    OnCompileRun(sender, null);
                    e.Handled = true;
                }
            }, RoutingStrategies.Tunnel);
        }

        private void CompileAndRun()
        {
            var selectedItem = LanguageSelector.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.IsEnabled)
            {
                var language = selectedItem.Content.ToString();
                var code = CodeInput.Text;
                var input = CodeInputParams.Text;
                string output = CompileAndRunCode(language, code, input);
                OutputBox.Text = output;
            }
            else
            {
                OutputBox.Text = "Выберите язык программирования.";
            }
        }

        private string CompileAndRunCode(string language, string code, string input)
        {
            string output = "";
            string filePath = _currentFilePath ?? Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

            try
            {
                if (_currentFilePath == null)
                {
                    File.WriteAllText(filePath, code);
                }

                string workingDirectory = _currentFilePath != null ? Path.GetDirectoryName(_currentFilePath) : Path.GetTempPath();

                switch (language)
                {
                    case "C#":
                        if (_currentFilePath == null)
                        {
                            var tempDirectory = Path.GetDirectoryName(filePath);
                            var csFilePath = Path.Combine(tempDirectory, "Program.cs");

                            Directory.CreateDirectory(tempDirectory);

                            using (var dotnetNew = new Process
                            {
                                StartInfo = new ProcessStartInfo("dotnet", "new console --output . --force")
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    WorkingDirectory = tempDirectory
                                }
                            })
                            {
                                dotnetNew.Start();
                                dotnetNew.WaitForExit();
                                if (dotnetNew.ExitCode != 0)
                                {
                                    output = "Project creation error: " + dotnetNew.StandardError.ReadToEnd();
                                    break;
                                }
                            }

                            var defaultProgramPath = Path.Combine(tempDirectory, "Program.cs");
                            if (File.Exists(defaultProgramPath))
                            {
                                File.Delete(defaultProgramPath);
                            }

                            File.WriteAllText(csFilePath, code);

                            using (var dotnetBuild = new Process
                            {
                                StartInfo = new ProcessStartInfo("dotnet", "build")
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    WorkingDirectory = tempDirectory
                                }
                            })
                            {
                                dotnetBuild.Start();
                                dotnetBuild.WaitForExit();
                                output += dotnetBuild.StandardOutput.ReadToEnd();
                                if (dotnetBuild.ExitCode != 0)
                                {
                                    output = "Build error: " + dotnetBuild.StandardError.ReadToEnd();
                                    break;
                                }
                            }

                            using (var dotnetRun = new Process
                            {
                                StartInfo = new ProcessStartInfo("dotnet", "run")
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    WorkingDirectory = tempDirectory
                                }
                            })
                            {
                                dotnetRun.Start();
                                output = dotnetRun.StandardOutput.ReadToEnd();
                                dotnetRun.WaitForExit();
                            }
                        }
                        else
                        {
                            using (var dotnetRun = new Process
                            {
                                StartInfo = new ProcessStartInfo("dotnet", $"run --project {_currentFilePath}")
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    WorkingDirectory = Path.GetDirectoryName(_currentFilePath)
                                }
                            })
                            {
                                dotnetRun.Start();
                                output = dotnetRun.StandardOutput.ReadToEnd();
                                dotnetRun.WaitForExit();
                                output += "\n" + dotnetRun.StandardError.ReadToEnd();
                            }
                        }
                        break;

                    case "C++":
                        string cppFilePath = filePath;
                        string outFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath));
                        using (var cppCompile = new Process
                        {
                            StartInfo = new ProcessStartInfo("c++", $"-o {outFilePath} {cppFilePath}")
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            cppCompile.Start();
                            cppCompile.WaitForExit();
                            if (cppCompile.ExitCode == 0)
                            {
                                using (var cppRun = new Process
                                {
                                    StartInfo = new ProcessStartInfo(outFilePath)
                                    {
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        WorkingDirectory = workingDirectory
                                    }
                                })
                                {
                                    cppRun.Start();
                                    output = cppRun.StandardOutput.ReadToEnd();
                                    cppRun.WaitForExit();
                                }
                            }
                            else
                            {
                                output = "Compilation error: " + cppCompile.StandardError.ReadToEnd();
                            }
                        }
                        break;

                    case "Python":
                        string pyFilePath = filePath;
                        using (var pythonRun = new Process
                        {
                            StartInfo = new ProcessStartInfo("python3", pyFilePath)
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                RedirectStandardInput = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            pythonRun.Start();
                            if (!string.IsNullOrEmpty(input))
                            {
                                using (StreamWriter sw = pythonRun.StandardInput)
                                {
                                    sw.WriteLine(input);
                                }
                            }
                            output = pythonRun.StandardOutput.ReadToEnd();
                            pythonRun.WaitForExit();
                            output += "\n" + pythonRun.StandardError.ReadToEnd();
                        }
                        break;

                    case "JavaScript":
                        string jsFilePath = filePath;
                        using (var jsRun = new Process
                        {
                            StartInfo = new ProcessStartInfo("node", jsFilePath)
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            jsRun.Start();
                            output = jsRun.StandardOutput.ReadToEnd();
                            jsRun.WaitForExit();
                            output += "\n" + jsRun.StandardError.ReadToEnd();
                        }
                        break;

                    case "Java":
                        if (_currentFilePath != null && _currentFilePath.EndsWith(".java"))
                        {
                            using (var javaCompile = new Process
                            {
                                StartInfo = new ProcessStartInfo("javac", _currentFilePath)
                                {
                                    RedirectStandardOutput = true,
                                    RedirectStandardError = true,
                                    UseShellExecute = false,
                                    CreateNoWindow = true,
                                    WorkingDirectory = workingDirectory
                                }
                            })
                            {
                                javaCompile.Start();
                                javaCompile.WaitForExit();
                                if (javaCompile.ExitCode == 0)
                                {
                                    string javaClassPath = Path.Combine(Path.GetDirectoryName(_currentFilePath), Path.GetFileNameWithoutExtension(_currentFilePath));
                                    using (var javaRun = new Process
                                    {
                                        StartInfo = new ProcessStartInfo("java", $"-cp {Path.GetDirectoryName(_currentFilePath)} {Path.GetFileNameWithoutExtension(_currentFilePath)}")
                                        {
                                            RedirectStandardOutput = true,
                                            RedirectStandardError = true,
                                            UseShellExecute = false,
                                            CreateNoWindow = true,
                                            WorkingDirectory = workingDirectory
                                        }
                                    })
                                    {
                                        javaRun.Start();
                                        output = javaRun.StandardOutput.ReadToEnd();
                                        javaRun.WaitForExit();
                                        output += "\n" + javaRun.StandardError.ReadToEnd();
                                    }
                                }
                                else
                                {
                                    output = "Compilation error: " + javaCompile.StandardError.ReadToEnd();
                                }
                            }
                        }
                        else
                        {
                            output = "Please open a .java file to compile and run.";
                        }
                        break;

                    case "C":
                        string cFilePath = filePath;
                        string cOutFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath));
                        using (var cCompile = new Process
                        {
                            StartInfo = new ProcessStartInfo("gcc", $"-o {cOutFilePath} {cFilePath}")
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            cCompile.Start();
                            cCompile.WaitForExit();
                            if (cCompile.ExitCode == 0)
                            {
                                using (var cRun = new Process
                                {
                                    StartInfo = new ProcessStartInfo(cOutFilePath)
                                    {
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        WorkingDirectory = workingDirectory
                                    }
                                })
                                {
                                    cRun.Start();
                                    output = cRun.StandardOutput.ReadToEnd();
                                    cRun.WaitForExit();
                                }
                            }
                            else
                            {
                                output = "Compilation error: " + cCompile.StandardError.ReadToEnd();
                            }
                        }
                        break;

                    case "Go":
                        string goFilePath = filePath;
                        string goOutFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath));
                        using (var goCompile = new Process
                        {
                            StartInfo = new ProcessStartInfo("go", $"build -o {goOutFilePath} {goFilePath}")
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            goCompile.Start();
                            goCompile.WaitForExit();
                            if (goCompile.ExitCode == 0)
                            {
                                using (var goRun = new Process
                                {
                                    StartInfo = new ProcessStartInfo(goOutFilePath)
                                    {
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        WorkingDirectory = workingDirectory
                                    }
                                })
                                {
                                    goRun.Start();
                                    output = goRun.StandardOutput.ReadToEnd();
                                    goRun.WaitForExit();
                                }
                            }
                            else
                            {
                                output = "Compilation error: " + goCompile.StandardError.ReadToEnd();
                            }
                        }
                        break;

                    case "Pascal":
                        string pasFilePath = filePath;
                        string pasOutFilePath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(filePath));
                        using (var pasCompile = new Process
                        {
                            StartInfo = new ProcessStartInfo("fpc", $"-o{pasOutFilePath} {pasFilePath}")
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            pasCompile.Start();
                            pasCompile.WaitForExit();
                            if (pasCompile.ExitCode == 0)
                            {
                                using (var pasRun = new Process
                                {
                                    StartInfo = new ProcessStartInfo(pasOutFilePath)
                                    {
                                        RedirectStandardOutput = true,
                                        RedirectStandardError = true,
                                        UseShellExecute = false,
                                        CreateNoWindow = true,
                                        WorkingDirectory = workingDirectory
                                    }
                                })
                                {
                                    pasRun.Start();
                                    output = pasRun.StandardOutput.ReadToEnd();
                                    pasRun.WaitForExit();
                                }
                            }
                            else
                            {
                                output = "Compilation error: " + pasCompile.StandardError.ReadToEnd();
                            }
                        }
                        break;

                    case "PHP":
                        string phpFilePath = filePath;
                        using (var phpRun = new Process
                        {
                            StartInfo = new ProcessStartInfo("php", phpFilePath)
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            phpRun.Start();
                            output = phpRun.StandardOutput.ReadToEnd();
                            phpRun.WaitForExit();
                            output += "\n" + phpRun.StandardError.ReadToEnd();
                        }
                        break;

                    case "Ruby":
                        string rubyFilePath = filePath;
                        using (var rubyRun = new Process
                        {
                            StartInfo = new ProcessStartInfo("ruby", rubyFilePath)
                            {
                                RedirectStandardOutput = true,
                                RedirectStandardError = true,
                                UseShellExecute = false,
                                CreateNoWindow = true,
                                WorkingDirectory = workingDirectory
                            }
                        })
                        {
                            rubyRun.Start();
                            output = rubyRun.StandardOutput.ReadToEnd();
                            rubyRun.WaitForExit();
                            output += "\n" + rubyRun.StandardError.ReadToEnd();
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                output += "An error occurred: " + ex.Message;
            }
            finally
            {
                if (_currentFilePath == null)
                {
                    try { File.Delete(filePath); } catch { }
                }
            }

            return output;
        }

        private void InitializeTextMate()
        {
            _registryOptions = new RegistryOptions(ThemeName.HighContrastLight);
            _textMateInstallation = CodeInput.InstallTextMate(_registryOptions);
            UpdateSyntaxHighlighting();
        }

        private void UpdateSyntaxHighlighting()
        {
            var extension = GetExtensionByLanguage();
            if (!string.IsNullOrEmpty(extension))
            {
                var language = _registryOptions.GetLanguageByExtension(extension);
                if (language != null)
                {
                    _textMateInstallation.SetGrammar(_registryOptions.GetScopeByLanguageId(language.Id));
                }
            }
        }

        private string GetExtensionByLanguage()
        {
            var selectedItem = LanguageSelector.SelectedItem as ComboBoxItem;
            if (selectedItem != null && selectedItem.IsEnabled)
            {
                var language = selectedItem.Content.ToString();
                switch (language)
                {
                    case "C#":
                        return ".cs";
                    case "C++":
                        return ".cpp";
                    case "Python":
                        return ".py";
                    case "JavaScript":
                        return ".js";
                    case "Java":
                        return ".java";
                    case "C":
                        return ".c";
                    case "Go":
                        return ".go";
                    case "Pascal":
                        return ".pas";
                    case "PHP":
                        return ".php";
                    case "Ruby":
                        return ".rb";
                    case "TypeScript":
                        return ".ts";
                    default:
                        return ".txt";
                }
            }
            return ".txt";
        }

        private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateSyntaxHighlighting();
        }

        private void CodeInput_TextChanged(object sender, EventArgs e)
        {
            _isTextChanged = true;
            UpdateTitle();
        }

        private void UpdateTitle()
        {
            if (_currentFilePath != null)
            {
                var fileName = Path.GetFileName(_currentFilePath);
                if (_isTextChanged)
                {
                    this.Title = $"(Не сохранено) {fileName} - Поликод МИРЭА - Desktop Compiler";
                }
                else
                {
                    this.Title = $"{fileName} - Поликод МИРЭА - Desktop Compiler";
                }
            }
            else
            {
                this.Title = "Поликод МИРЭА - Desktop Compiler";
            }
        }
    }
}
