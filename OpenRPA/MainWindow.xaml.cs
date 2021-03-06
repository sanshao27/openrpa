﻿using Newtonsoft.Json.Linq;
using OpenRPA.Input;
using OpenRPA.Interfaces;
using OpenRPA.Net;
using System;
using System.Activities.Core.Presentation;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace OpenRPA
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public System.Collections.ObjectModel.ObservableCollection<Project> Projects { get; set; } = new System.Collections.ObjectModel.ObservableCollection<Project>();
        private bool isRecording = false;
        private bool autoReconnect = true;
        public static Tracing tracing = new Tracing();
        public MainWindow()
        {
            InitializeComponent();
            GenericTools.mainWindow = this;
            System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level = System.Diagnostics.SourceLevels.Critical;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            AppDomain currentDomain = AppDomain.CurrentDomain;
            System.Windows.Forms.Application.ThreadException += new System.Threading.ThreadExceptionEventHandler(Application_ThreadException);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);
            // .WriteTo.Console()
            //Log.Logger = new LoggerConfiguration().MinimumLevel.Debug()
            //    .WriteTo.File("log-.txt", rollingInterval: RollingInterval.Day)
            //    .WriteTo.OpenRPATracing(tracing)
            //    .CreateLogger();
            System.Diagnostics.Trace.Listeners.Add(tracing);
            Console.SetOut(new DebugTextWriter());
            lvDataBinding.ItemsSource = Plugins.recordPlugins;

        }
        static void Application_ThreadException(object sender, System.Threading.ThreadExceptionEventArgs e)
        {
            Log.Error(e.Exception, "");
        }
        static void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception ex = (Exception)args.ExceptionObject;
            Log.Error(ex, "");
            Log.Error("MyHandler caught : " + ex.Message);
            Log.Error("Runtime terminating: {0}", (args.IsTerminating).ToString());
        }
        private void AddHotKeys()
        {
            AutomationHelper.syncContext.Post(o =>
            {
                try
                {
                    RoutedCommand saveHotkey = new RoutedCommand();
                    saveHotkey.InputGestures.Add(new KeyGesture(Key.S, ModifierKeys.Control));
                    CommandBindings.Add(new CommandBinding(saveHotkey, onSave));
                    RoutedCommand deleteHotkey = new RoutedCommand();
                    deleteHotkey.InputGestures.Add(new KeyGesture(Key.Delete));
                    CommandBindings.Add(new CommandBinding(deleteHotkey, onDelete));
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    MessageBox.Show(ex.Message);
                }
            }, null);
        }
        private void onSave(object sender, ExecutedRoutedEventArgs e)
        {
            SaveCommand.Execute(mainTabControl.SelectedContent);
        }
        private void onDelete(object sender, ExecutedRoutedEventArgs e)
        {
            DeleteCommand.Execute(mainTabControl.SelectedContent);
        }
        public ICommand SettingsCommand { get { return new RelayCommand<object>(onSettings, canSettings); } }
        public ICommand SignoutCommand { get { return new RelayCommand<object>(onSignout, canSignout); } }
        
        public ICommand OpenCommand { get { return new RelayCommand<object>(onOpen, canOpen); } }
        public ICommand SaveCommand { get { return new RelayCommand<object>(onSave, canSave); } }
        public ICommand NewCommand { get { return new RelayCommand<object>(onNew, canNew); } }
        public ICommand DeleteCommand { get { return new RelayCommand<object>(onDelete, canDelete); } }
        public ICommand PlayCommand { get { return new RelayCommand<object>(onPlay, canPlay); } }
        public ICommand StopCommand { get { return new RelayCommand<object>(onStop, canStop); } }
        public ICommand RecordCommand { get { return new RelayCommand<object>(onRecord, canRecord); } }
        private bool canSettings(object item)
        {
            return true;
        }
        private void onSettings(object item)
        {
            try
            {
                var filename = "settings.json";
                var path = System.IO.Directory.GetCurrentDirectory();
                string settingsFile = System.IO.Path.Combine(path, filename);
                var process = new System.Diagnostics.Process();
                process.StartInfo = new System.Diagnostics.ProcessStartInfo()
                {
                    UseShellExecute = true,
                    FileName = settingsFile
                };
                process.Start();
            }
            catch (Exception ex)
            {
                Log.Error("onSettings: " + ex.Message);
            }
        }
        private bool canSignout(object item)
        {
            if (!global.isConnected) return false;
            return true;
        }
        private void onSignout(object item)
        {
            autoReconnect = true;
            Projects.Clear();
            Config.local.password = Config.local.ProtectString("BadPassword");
            _ = global.webSocketClient.Close();
        }
        private bool canOpen(object item)
        {
            if (!isConnected) return false;
            foreach (TabItem tab in mainTabControl.Items)
            {
                if (tab.Content is Views.OpenProject)
                {
                    return false;
                }
            }
            return true;
        }
        private void onOpen(object item)
        {
            AutomationHelper.syncContext.Post(o =>
            {
                foreach (TabItem tab in mainTabControl.Items)
                {
                    if (tab.Content is Views.OpenProject)
                    {
                        tab.IsSelected = true;
                        return;
                    }
                }
                var view = new Views.OpenProject(this);
                view.onOpenProject += onOpenProject;
                view.onOpenWorkflow += onOpenWorkflow;
                Views.ClosableTab newTabItem = new Views.ClosableTab
                {
                    Title = "Open project",
                    Name = "openproject",
                    Content = view
                };
                newTabItem.OnClose += NewTabItem_OnClose;
                mainTabControl.Items.Add(newTabItem);
                newTabItem.IsSelected = true;
            }, null);
        }
        private async void NewTabItem_OnClose(object sender, RoutedEventArgs e)
        {
            Views.ClosableTab tab = sender as Views.ClosableTab;
            Views.WFDesigner designer = tab.Content as Views.WFDesigner;
            if (designer == null) return;
            if (!designer.HasChanged) return;
            if (designer.HasChanged && (global.isConnected?global.webSocketClient.user.hasRole("robot admins"):true))
            {
                MessageBoxResult messageBoxResult = System.Windows.MessageBox.Show("Save " + designer.Workflow.name + " ?", "Workflow unsaved", MessageBoxButton.YesNoCancel);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    await designer.Save();
                }
                else if (messageBoxResult != MessageBoxResult.No)
                {
                    e.Handled = true;
                }
            }
        }
        public Views.WFDesigner getWorkflowDesignerByFilename(string Filename)
        {
            Views.WFDesigner designer = null;
            foreach (TabItem tab in mainTabControl.Items)
            {
                if (tab.Content is Views.WFDesigner)
                {
                    designer = (Views.WFDesigner)tab.Content;
                    if (designer.Workflow.FilePath == Filename)
                    {
                        return designer;
                    }
                }
            }
            return null;
        }
        public Views.WFDesigner getWorkflowDesignerById(string Id)
        {
            Views.WFDesigner designer = null;
            foreach (TabItem tab in mainTabControl.Items)
            {
                if (tab.Content is Views.WFDesigner)
                {
                    designer = (Views.WFDesigner)tab.Content;
                    if (designer.Workflow._id == Id)
                    {
                        return designer;
                    }
                }
            }
            return null;
        }
        public void onOpenWorkflow(Workflow workflow)
        {
            AutomationHelper.syncContext.Post(o =>
            {
                Views.WFDesigner designer = getWorkflowDesignerByFilename(workflow.FilePath);
                if (designer == null && !string.IsNullOrEmpty(workflow._id)) designer = getWorkflowDesignerById(workflow._id);
                if (designer != null)
                {
                    designer.tab.IsSelected = true;
                    return;
                }
                try
                {
                    Views.ClosableTab newTabItem = new Views.ClosableTab
                    {
                        Title = "Open project",
                        Name = "openproject"
                    };
                    newTabItem.OnClose += NewTabItem_OnClose;
                    var types = new List<Type>();
                    foreach (var p in Plugins.recordPlugins) { types.Add(p.GetType()); }
                    var view = new Views.WFDesigner((Views.ClosableTab)newTabItem, workflow, types.ToArray());
                    view.onChanged += WFDesigneronChanged;
                    newTabItem.Content = view;
                    mainTabControl.Items.Add(newTabItem);
                    newTabItem.IsSelected = true;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    MessageBox.Show(ex.Message);
                }
            }, null);
        }
        private void WFDesigneronChanged(Views.WFDesigner view)
        {
            AutomationHelper.syncContext.Post(o =>
            {
                Views.WFDesigner designer = null;
                foreach (TabItem tab in mainTabControl.Items)
                {
                    if (tab.Content is Views.WFDesigner)
                    {
                        designer = (Views.WFDesigner)tab.Content;
                        if (designer.Workflow.FilePath == view.Workflow.FilePath)
                        {
                            var t = (Views.ClosableTab)tab;
                            t.Title = (designer.HasChanged ? designer.Workflow.name + "*" : designer.Workflow.name);
                            CommandManager.InvalidateRequerySuggested();
                            return;
                        }
                    }
                }
            }, null);
            //_syncContext.Post(o => CommandManager.InvalidateRequerySuggested(), null);
        }
        public void onOpenProject(Project project)
        {
            foreach (var wf in project.Workflows)
            {
                onOpenWorkflow(wf);
            }
        }
        private bool canSave(object item) { return (item is Views.WFDesigner); }
        private async void onSave(object item)
        {
            if (item is Views.WFDesigner)
            {
                var designer = (Views.WFDesigner)item;
                await designer.Save();
            }
            if (item is Views.OpenProject)
            {
                var view = (Views.OpenProject)item;
                var Project = view.listWorkflows.SelectedItem as Project;
                if (Project != null)
                {
                    await Project.Save();
                }
            }
        }
        private bool canNew(object item) { if (!isConnected) return false; return (item is Views.WFDesigner || item is Views.OpenProject || item == null); }
        private async void onNew(object item)
        {
            try
            {
                if (item is Views.WFDesigner)
                {
                    var designer = (Views.WFDesigner)item;
                    Workflow workflow = Workflow.Create(designer.Project, "New Workflow");
                    onOpenWorkflow(workflow);
                    return;
                }
                else
                {
                    string Name = Microsoft.VisualBasic.Interaction.InputBox("Name?", "Name project", "New project");
                    if (string.IsNullOrEmpty(Name)) return;
                    Project project = await Project.Create(Extensions.projectsDirectory, Name);
                    Workflow workflow = project.Workflows.First();
                    workflow.Project = project;
                    Projects.Add(project);
                    onOpenWorkflow(workflow);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "");
                MessageBox.Show(ex.Message);
            }
        }
        private bool canDelete(object item)
        {
            var view = item as Views.OpenProject;
            if (view == null) return false;
            var val = view.listWorkflows.SelectedValue;
            if (val == null) return false;
            return true;
        }

        private async void onDelete(object item)
        {
            var view = item as Views.OpenProject;
            if (view == null) return;
            var val = view.listWorkflows.SelectedValue;
            var wf = val as Workflow;
            var p = val as Project;


            if (wf != null)
            {
                Views.WFDesigner designer = getWorkflowDesignerByFilename(wf.FilePath);
                if (designer == null && !string.IsNullOrEmpty(wf._id)) { designer = getWorkflowDesignerById(wf._id); }
                if (designer != null) { designer.tab.Close(); }

                var messageBoxResult = MessageBox.Show("Delete " + wf.name + " ?", "Delete Confirmation", MessageBoxButton.YesNo);
                if (messageBoxResult != MessageBoxResult.Yes) return;

                await wf.Delete();
            }
            if (p != null)
            {
                if (p.Workflows.Count > 0)
                {
                    var messageBoxResult = MessageBox.Show("Delete project " + p.name + " containing " + p.Workflows.Count() + " workflows", "Delete Confirmation", MessageBoxButton.YesNo);
                    if (messageBoxResult != MessageBoxResult.Yes) return;
                    foreach (var _wf in p.Workflows.ToList())
                    {
                        Views.WFDesigner designer = getWorkflowDesignerByFilename(_wf.FilePath);
                        if (designer == null && !string.IsNullOrEmpty(_wf._id)) { designer = getWorkflowDesignerById(_wf._id); }
                        if (designer != null) { designer.tab.Close(); }
                        await _wf.Delete();
                    }
                }
                await p.Delete();
                Projects.Remove(p);
            }
        }
        private bool canPlay(object item)
        {
            if (!isConnected) return false;
            if (isRecording) return false;
            if (!(item is Views.WFDesigner)) return false;
            var designer = (Views.WFDesigner)item;
            foreach (var i in designer.Workflow.Instances)
            {
                if (i.isCompleted == false)
                {
                    return false;
                }
            }
            return true;
        }
        private async void onPlay(object item)
        {
            if (!(item is Views.WFDesigner)) return;
            var designer = (Views.WFDesigner)item;
            if (designer.HasChanged) { await designer.Save(); }
            GenericTools.minimize(GenericTools.mainWindow);

            var param = new Dictionary<string, object>();
            await designer.Workflow.Run(param, null, null, onIdle);
            return;
        }
        private bool canStop(object item)
        {
            if (!isConnected) return false;
            if (isRecording) return true;
            if (!(item is Views.WFDesigner)) return false;
            var designer = (Views.WFDesigner)item;
            foreach (var i in designer.Workflow.Instances)
            {
                if (i.isCompleted != true)
                {
                    return true;
                }
            }
            return false;
        }
        private void onStop(object item)
        {
            if (!(item is Views.WFDesigner)) return;
            var designer = (Views.WFDesigner)item;
            foreach (var i in designer.Workflow.Instances)
            {
                if (i.isCompleted == false)
                {
                    i.Abort("User clicked stop");
                }
            }
            if (isRecording)
            {
                StopRecordPlugins();
                InputDriver.Instance.CallNext = true;
                InputDriver.Instance.OnKeyDown -= OnKeyDown;
                InputDriver.Instance.OnKeyUp -= OnKeyUp;
                GenericTools.restore(GenericTools.mainWindow);
            }
        }
        private bool canRecord(object item)
        {
            if (!isConnected) return false;
            if (!(item is Views.WFDesigner)) return false;
            var designer = (Views.WFDesigner)item;
            foreach (var i in designer.Workflow.Instances)
            {
                if (i.isCompleted == false)
                {
                    return false;
                }
            }
            return !isRecording;
        }
        private void OnKeyDown(Input.InputEventArgs e)
        {
            if (!isRecording) return;
            if(e.Key == KeyboardKey.Escape)
            {
                StopRecordPlugins();
                InputDriver.Instance.CallNext = true;
                InputDriver.Instance.OnKeyDown -= OnKeyDown;
                InputDriver.Instance.OnKeyUp -= OnKeyUp;
                GenericTools.restore(GenericTools.mainWindow);
                return;
            }
            // if (e.Key == KeyboardKey. 255) return;
            try
            {
                if (mainTabControl.SelectedContent is Views.WFDesigner view)
                {
                    if (view.lastinserted != null && view.lastinserted is Activities.TypeText)
                    {
                        Log.Debug("re-use existing TypeText");
                        var item = (Activities.TypeText)view.lastinserted;
                        item.AddKey(new Activities.vKey((FlaUI.Core.WindowsAPI.VirtualKeyShort)e.Key, false), view.lastinsertedmodel);
                    }
                    else
                    {
                        Log.Debug("Add new TypeText");
                        var rme = new Activities.TypeText();
                        view.lastinsertedmodel = view.addActivity(rme);
                        rme.AddKey(new Activities.vKey((FlaUI.Core.WindowsAPI.VirtualKeyShort)e.Key, false), view.lastinsertedmodel);
                        view.lastinserted = rme;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }

        }
        private void OnKeyUp(Input.InputEventArgs e)
        {
            if (!isRecording) return;
            // if (e.KeyValue == 255) return;
            try
            {
                if (mainTabControl.SelectedContent is Views.WFDesigner view)
                {
                    if (view.lastinserted != null && view.lastinserted is Activities.TypeText)
                    {
                        Log.Debug("re-use existing TypeText");
                        var item = (Activities.TypeText)view.lastinserted;
                        item.AddKey(new Activities.vKey((FlaUI.Core.WindowsAPI.VirtualKeyShort)e.Key, true), view.lastinsertedmodel);
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
            }
        }
        private void StartRecordPlugins()
        {
            isRecording = true;
            var p = Plugins.recordPlugins.Where(x => x.Name == "Windows").First();
            p.OnUserAction += OnUserAction;
            p.Start();
        }
        private void StopRecordPlugins()
        {
            isRecording = false;
            var p = Plugins.recordPlugins.Where(x => x.Name == "Windows").First();
            p.OnUserAction -= OnUserAction;
            p.Stop();
        }
        public void OnUserAction(IPlugin sender, IRecordEvent e)
        {
            StopRecordPlugins();
            AutomationHelper.syncContext.Post(o =>
            {
                foreach (var p in Plugins.recordPlugins)
                {
                    if (p.Name != sender.Name)
                    {
                        if (p.parseUserAction(ref e)) continue;
                    }
                }
                if(e.a == null)
                {
                    StartRecordPlugins();
                    if (e.ClickHandled == false)
                    {
                        InputDriver.Instance.CallNext = true;
                        Log.Debug("MouseMove to " + e.X + "," + e.Y + " and click " + e.Button + " button");
                        InputDriver.Instance.MouseMove(e.X, e.Y);
                        // InputDriver.Instance.Click(lastInputEventArgs.Button);
                        InputDriver.DoMouseClick();
                        Log.Debug("Click done");
                    }
                    return;
                }
                InputDriver.Instance.CallNext = true;
                if (mainTabControl.SelectedContent is Views.WFDesigner view)
                {
                    e.a.addActivity(new Activities.ClickElement
                    {
                        Element = new System.Activities.InArgument<IElement>()
                        {
                            Expression = new Microsoft.VisualBasic.Activities.VisualBasicValue<IElement>("item")
                        }
                    }, "item");
                    if (e.SupportInput)
                    {
                        var win = new Views.InsertText();
                        win.Topmost = true;
                        isRecording = false;
                        if (win.ShowDialog() == true)
                        {
                            e.a.addActivity(new System.Activities.Statements.Assign<string>
                            {
                                // TODO: use assign 
                                //To = new VisualBasicReference<string>("item.Text")
                                To = new Microsoft.VisualBasic.Activities.VisualBasicReference<string>("item.value"),
                                Value = win.Text
                            }, "item");
                            e.Element.Value = win.Text;
                        } else { e.SupportInput = false;  }
                        isRecording = true;
                    }
                    view.lastinserted = e.a.Activity;
                    view.lastinsertedmodel = view.addActivity(e.a.Activity);
                    if(e.ClickHandled == false && e.SupportInput == false)
                    {
                        InputDriver.Instance.CallNext = true;
                        Log.Debug("MouseMove to " + e.X + "," + e.Y + " and click " + e.Button + " button");
                        InputDriver.Instance.MouseMove(e.X, e.Y);
                        // InputDriver.Instance.Click(lastInputEventArgs.Button);
                        InputDriver.DoMouseClick();
                        Log.Debug("Click done");
                    }
                    System.Threading.Thread.Sleep(200);
                }
                InputDriver.Instance.CallNext = false;
                StartRecordPlugins();
            }, null);
        }
        private void onRecord(object item)
        {
            if (!(item is Views.WFDesigner)) return;
            var designer = (Views.WFDesigner)item;
            designer.lastinserted = null;
            designer.lastinsertedmodel = null;
            InputDriver.Instance.OnKeyDown += OnKeyDown;
            InputDriver.Instance.OnKeyUp += OnKeyUp;
            StartRecordPlugins();
            InputDriver.Instance.CallNext = false;
            GenericTools.minimize(GenericTools.mainWindow);
        }
        private async void WebSocketClient_OnClose(string reason)
        {
            Log.Information("Disconnected " + reason);
            AutomationHelper.syncContext.Post(o =>
            {
                LabelStatusBar.Content = "Disconnected from " + Config.local.wsurl + " reason " + reason;
            }, null);
            await Task.Delay(1000);
            if(autoReconnect) _ = global.webSocketClient.Connect();
        }
        private bool loginInProgress = false;
        private void WebSocketClient_OnOpen()
        {
            AutomationHelper.syncContext.Post(async o =>
            {
                var sw = new System.Diagnostics.Stopwatch();
                sw.Start();
                Log.Debug("WebSocketClient_OnOpen::begin " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                LabelStatusBar.Content = "Connected to " + Config.local.wsurl;
                TokenUser user = null;
                while (user == null)
                {
                    string errormessage = string.Empty;
                    if (!string.IsNullOrEmpty(Config.local.username))
                    {
                        try
                        {
                            LabelStatusBar.Content = "Connected to " + Config.local.wsurl + " signing in as " + Config.local.username + " ...";
                            Log.Debug("Signing in as " + Config.local.username + " " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                            user = await global.webSocketClient.Signin(Config.local.username, Config.local.UnprotectString(Config.local.password));
                            Log.Debug("Signed in as " + Config.local.username + " " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                            LabelStatusBar.Content = "Connected to " + Config.local.wsurl + " as " + user.name;
                        }
                        catch (Exception ex)
                        {
                            this.Hide();
                            Log.Error(ex, "");
                            errormessage = ex.Message;
                        }
                    }
                    if (user == null)
                    {
                        if(loginInProgress==false)
                        {
                            loginInProgress = true;
                            var w = new Views.LoginWindow();
                            w.username = Config.local.username;
                            w.errormessage = errormessage;
                            w.fqdn = new Uri(Config.local.wsurl).Host;
                            this.Hide();
                            if (w.ShowDialog() != true) { this.Show(); return; }
                            Config.local.username = w.username; Config.local.password = Config.local.ProtectString(w.password);
                            Config.Save();
                            loginInProgress = false;
                        } 
                        else
                        {
                            return;
                        }
                    }
                }
                this.Show();
                lvDataBinding.ItemsSource = Plugins.recordPlugins;

                _ = Task.Run(async () =>
                  {
                      try
                      {
                          var host = Environment.MachineName.ToLower();
                          var fqdn = System.Net.Dns.GetHostEntry(Environment.MachineName).HostName.ToLower();
                          Log.Debug("Registering robot in robot." + Config.local.username + " queue " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                          await global.webSocketClient.RegisterQueue("robot." + Config.local.username);
                          Log.Debug("Registering robot in robot." + fqdn + " queue " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                          await global.webSocketClient.RegisterQueue("robot." + fqdn);
                          Log.Debug("Registering robot conplete " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                      }
                      catch (Exception ex)
                      {
                          Log.Error("Error RegisterQueue" + ex.ToString());
                      }
                  });

                try
                {


                    if (Projects.Count != 0) return;

                    Log.Debug("Get workflows from server " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                    var workflows = await global.webSocketClient.Query<Workflow>("openrpa", "{_type: 'workflow'}");
                    Log.Debug("Get projects from server " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                    var projects = await global.webSocketClient.Query<Project>("openrpa", "{_type: 'project'}");
                    Log.Debug("Done getting workflows and projects " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));


                    var folders = new List<string>();
                    foreach (var p in projects)
                    {
                        p.Path = System.IO.Path.Combine(Extensions.projectsDirectory, p.name);
                        if (folders.Contains(p.Path))
                        {
                            p.Path = System.IO.Path.Combine(Extensions.projectsDirectory, p._id);
                        }
                        folders.Add(p.Path);
                    }

                    foreach (var p in projects)
                    {
                        p.Workflows = new System.Collections.ObjectModel.ObservableCollection<Workflow>();
                        foreach (var workflow in workflows)
                        {
                            if (workflow.projectid == p._id)
                            {
                                workflow.Project = p;
                                p.Workflows.Add(workflow);
                            }
                        }
                        Log.Debug("Saving project " + p.name + " " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                        p.SaveFile();
                        Projects.Add(p);
                    }
                    Log.Debug("RunPendingInstances::begin " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                    foreach (var workflow in workflows)
                    {
                        await workflow.RunPendingInstances();
                    }
                    Log.Debug("RunPendingInstances::end " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                    if (workflows.Count() == 0 && projects.Count() == 0)
                    {
                        var _Projects = Project.loadProjects(Extensions.projectsDirectory);
                        if (_Projects.Count() > 0)
                        {
                            foreach (var _project in _Projects)
                            {
                                var p = await global.webSocketClient.InsertOne("openrpa", _project);
                                p.Workflows = new System.Collections.ObjectModel.ObservableCollection<Workflow>();
                                p.Path = System.IO.Path.Combine(Extensions.projectsDirectory, p.name);
                                Projects.Add(p);
                                foreach (var _workflow in _project.Workflows)
                                {
                                    _workflow.projectid = p._id;
                                    var w = await global.webSocketClient.InsertOne("openrpa", _workflow);
                                    w.Project = p;
                                    p.Workflows.Add(w);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "");
                    MessageBox.Show("WebSocketClient_OnOpen::Sync projects " + ex.Message);
                }
                Log.Debug("WebSocketClient_OnOpen::end " + string.Format("{0:mm\\:ss\\.fff}", sw.Elapsed));
                LabelStatusBar.Content = "Connected to " + Config.local.wsurl + " as " + user.name;
                if (Projects.Count > 0)
                {
                    Log.Debug("Opening first project");
                    onOpenProject(Projects[0]);
                }
            }, null);
        }

        public bool usingOpenFlow
        {
            get
            {
                return !string.IsNullOrEmpty(Config.local.wsurl);
            }
        }
        public bool isConnected
        {
            get
            {
                if (!usingOpenFlow) return true; // IF working offline, were allways connected, right ?
                if (global.webSocketClient == null) return false;
                return global.webSocketClient.isConnected;
            }
        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AutomationHelper.syncContext = System.Threading.SynchronizationContext.Current;
            DataContext = this;
            if (!string.IsNullOrEmpty(Config.local.wsurl))
            {
                LabelStatusBar.Content = "Connecting to " + Config.local.wsurl;
            }
            Plugins.loadPlugins(Extensions.projectsDirectory);
            Task.Run(() =>
            {
                ExpressionEditor.EditorUtil.init();

                if (!string.IsNullOrEmpty(Config.local.wsurl))
                {
                    global.webSocketClient = new WebSocketClient(Config.local.wsurl);
                    global.webSocketClient.OnOpen += WebSocketClient_OnOpen;
                    global.webSocketClient.OnClose += WebSocketClient_OnClose;
                    global.webSocketClient.OnQueueMessage += WebSocketClient_OnQueueMessage;
                    
                    _ = global.webSocketClient.Connect();
                }
                else
                {
                    var _Projects = Project.loadProjects(Extensions.projectsDirectory);
                    Projects = new System.Collections.ObjectModel.ObservableCollection<Project>();
                    foreach (Project p in _Projects)
                    {
                        Projects.Add(p);
                    }
                }
                AutomationHelper.init();
                new DesignerMetadata().Register();
                onOpen(null);
                if (Projects.Count > 0)
                {
                    onOpenWorkflow(Projects[0].Workflows.First());
                }
                AddHotKeys();
            });
        }

        private Workflow GetWorkflowById(string id)
        {
            foreach(var p in Projects)
            {
                foreach(var wf in p.Workflows)
                {
                    if (wf._id == id) return wf;
                }
            }
            return null;
        }
        private void onIdle(WorkflowInstance instance, EventArgs e)
        {
            if (!string.IsNullOrEmpty(instance.queuename) && !string.IsNullOrEmpty(instance.correlationId))
            {
                RobotCommand command = new RobotCommand();
                var data = JObject.FromObject(instance.Parameters);
                command.command = "invoke" + instance.state;
                command.workflowid = instance.WorkflowId;
                command.data = data;
                _ = global.webSocketClient.QueueMessage(instance.queuename, command, instance.correlationId);
            } else
            {
                if(instance.state != "idle")
                {
                    GenericTools.restore(GenericTools.mainWindow);
                }
            }

        }
        private async void WebSocketClient_OnQueueMessage(QueueMessage message)
        {
            RobotCommand command = null;
            try
            {
                command = Newtonsoft.Json.JsonConvert.DeserializeObject<RobotCommand>(message.data.ToString());
                var data = JObject.Parse(command.data.ToString());
                if (command.command == "invoke")
                {
                    var workflow = GetWorkflowById(command.workflowid);
                    if (workflow == null) throw new Exception("Unknown workflow " + command.workflowid);
                    var param = new Dictionary<string, object>();
                    foreach(var k in data)
                    {
                        switch (k.Value.Type)
                        {
                            case JTokenType.Integer: param.Add(k.Key, k.Value.Value<int>()); break;
                            case JTokenType.Float: param.Add(k.Key, k.Value.Value<float>()); break;
                            case JTokenType.Boolean: param.Add(k.Key, k.Value.Value<bool>()); break;
                            case JTokenType.Date: param.Add(k.Key, k.Value.Value<DateTime>()); break;
                            case JTokenType.TimeSpan: param.Add(k.Key, k.Value.Value<TimeSpan>()); break;
                            default: param.Add(k.Key, k.Value.Value<string>()); break;
                        }
                    }
                    await workflow.Run(param, message.replyto, message.correlationId, onIdle);
                    command.command = "invokesuccess";
                }
            }
            catch (Exception ex)
            {
                command = new RobotCommand();
                command.command = "error";
                command.data = JObject.FromObject(ex);
            }
            // string data = Newtonsoft.Json.JsonConvert.SerializeObject(command);
            if(message.replyto != message.queuename)
            {
                await global.webSocketClient.QueueMessage(message.replyto, command, message.correlationId);
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            // automation threads will not allways abort, and mousemove hook will "hang" the application for several seconds
            Environment.Exit(Environment.ExitCode);

        }
    }
}
