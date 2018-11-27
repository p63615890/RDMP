using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CatalogueLibrary;
using CatalogueLibrary.Data.DataLoad;
using CatalogueLibrary.Repositories;
using CatalogueManager.Icons.IconOverlays;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using ReusableLibraryCode.CommandExecution.AtomicCommands;
using ReusableLibraryCode.Icons.IconProvision;
using ReusableUIComponents;

namespace CatalogueManager.CommandExecution.AtomicCommands
{
    internal class ExecuteCommandCreateNewProcessTask : BasicUICommandExecution, IAtomicCommand
    {
        private readonly ProcessTaskType _taskType;
        private readonly LoadMetadata _loadMetadata;
        private readonly LoadStage _loadStage;
        private Bitmap _image;
        private HICProjectDirectory _hicProjectDirectory;
        private FileInfo _file;

        public ExecuteCommandCreateNewProcessTask(IActivateItems activator, ProcessTaskType taskType, LoadMetadata loadMetadata, LoadStage loadStage, FileInfo file=null) : base(activator)
        {
            _taskType = taskType;
            _loadMetadata = loadMetadata;
            _loadStage = loadStage;

            try
            {
                _hicProjectDirectory = new HICProjectDirectory(_loadMetadata.LocationOfFlatFiles);
            }
            catch (Exception)
            {
                SetImpossible("Could not construct HICProjectDirectory");
            }
            
            if(taskType == ProcessTaskType.SQLFile)
            {
                _image = activator.CoreIconProvider.GetImage(RDMPConcept.SQL, OverlayKind.Add);
            }
            else if(taskType == ProcessTaskType.Executable)
            {
                _image = new IconOverlayProvider().GetOverlayNoCache(CatalogueIcons.Exe, OverlayKind.Add);
            }
            else 
                SetImpossible("Only SQLFile and Executable task types are supported by this command");

            if (!ProcessTask.IsCompatibleStage(taskType, loadStage))
                SetImpossible("You cannot run "+taskType+" in " + loadStage);

            _file = file;
        }

        public override void Execute()
        {
            base.Execute();

            if (_file == null)
            {
                if (_taskType == ProcessTaskType.SQLFile)
                {
                        var dialog = new TypeTextOrCancelDialog("Enter a name for the SQL file", "File name", 100, "myscript.sql");
                        if (dialog.ShowDialog() == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.ResultText))
                        {
                            var target = Path.Combine(_hicProjectDirectory.ExecutablesPath.FullName, dialog.ResultText);

                            if (!target.EndsWith(".sql"))
                                target += ".sql";

                            //create it if it doesn't exist
                            if (!File.Exists(target))
                                File.WriteAllText(target, "/*todo Type some SQL*/");

                            _file = new FileInfo(target);
                        }
                        else
                            return; //user cancelled
                }
                else if (_taskType == ProcessTaskType.Executable)
                {
                    var dialog = new OpenFileDialog();
                    dialog.Filter = "Executables|*.exe";
                    dialog.CheckFileExists = true;

                    if (dialog.ShowDialog() == DialogResult.OK)
                        _file = new FileInfo(dialog.FileName);
                    else
                        return;
                }
                else
                    throw new ArgumentOutOfRangeException("Unexpected _taskType:" + _taskType);
            }

            var task = new ProcessTask((ICatalogueRepository)_loadMetadata.Repository, _loadMetadata, _loadStage);
            task.ProcessTaskType = _taskType;
            task.Path = _file.FullName;
            SaveAndShow(task);
        }

        private void SaveAndShow(ProcessTask task)
        {
            task.Name = "Run '" + Path.GetFileName(task.Path) +"'";
            task.SaveToDatabase();

            Publish(_loadMetadata);
            Activate(task);
        }

        public override string GetCommandName()
        {
            switch (_taskType)
            {
                case ProcessTaskType.Executable:
                    return "Add New Run .exe File Task";
                case ProcessTaskType.SQLFile:
                    return "Add New Run SQL Script Task";
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Image GetImage(IIconProvider iconProvider)
        {
            return _image;
        }
    }
}