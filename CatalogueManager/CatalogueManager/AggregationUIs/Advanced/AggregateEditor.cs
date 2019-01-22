using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;
using BrightIdeasSoftware;
using CatalogueLibrary.Data;
using CatalogueLibrary.Data.Aggregation;
using CatalogueLibrary.Data.Cohort.Joinables;
using CatalogueLibrary.QueryBuilding;
using CatalogueLibrary.QueryBuilding.Options;
using CatalogueLibrary.Repositories;
using CatalogueManager.AutoComplete;
using CatalogueManager.CommandExecution.AtomicCommands;
using CatalogueManager.DataViewing.Collections;
using CatalogueManager.ExtractionUIs.FilterUIs.ParameterUIs;
using CatalogueManager.ExtractionUIs.FilterUIs.ParameterUIs.Options;
using CatalogueManager.Icons.IconProvision;
using CatalogueManager.ItemActivation;
using CatalogueManager.ItemActivation.Emphasis;
using CatalogueManager.Refreshing;
using CatalogueManager.SimpleControls;
using CatalogueManager.TestsAndSetup.ServicePropogation;
using FAnsi.Discovery.QuerySyntax;
using FAnsi.Discovery.QuerySyntax.Aggregation;
using MapsDirectlyToDatabaseTable;
using MapsDirectlyToDatabaseTable.Revertable;
using CatalogueManager.Copying;
using ReusableUIComponents;
using ReusableUIComponents.Dialogs;
using ReusableUIComponents.ScintillaHelper;
using ScintillaNET;

namespace CatalogueManager.AggregationUIs.Advanced
{
    /// <summary>
    /// Allows you to adjust an Aggregate.  This can either be a breakdown of your dataset by columns possibly including a graph (Basic Aggregate), a list of patient identifiers (Identifier 
    /// List) or a patient index table (See AggregateConfiguration). The image in the top left tells you what type of AggregateConfiguration it is.
    ///  
    /// <para>Clicking the 'Parameters' button will launch the ParameterCollectionUI dialogue which will let you edit which SQL Parameters @startDate etc are available for use in filters on the 
    /// AggregateConfiguration</para>
    /// 
    /// <para>If you are editing a Basic Aggregate that does not include any patient identifier columns (IsExtractionIdentifier) then you can tick IsExtractable to make it available for use and
    /// extraction for researchers who use the underlying dataset and receive a data extraction (they will receive the 'master' aggregate run on the entire data repository and a 'personal'
    /// version which is the same query run against their project extraction only) See ExtractionAggregateGraphObjectCollection.</para>
    /// 
    /// <para>You can click in the SQL and Alias columns to rename columns or change their SQL.  You can also click in the 'Join Direction' column to edit the direction (LEFT or RIGHT) of 
    /// any supplemental JOINs.</para>
    /// 
    /// <para>If your Catalogue has multiple underlying TableInfos you can pick which ones to include in the query generated in the FROM section (any Columns included in the SELECT section
    /// will be automatically included)</para>
    /// 
    /// <para>Typing into the HAVING block will make the Query Builder add the SQL into the HAVING section of a GROUP BY SQL statement</para>
    /// 
    /// <para>You can (if it is a Basic Aggregate) choose a single column to PIVOT on.  This will turn row values into new column headers.  For example if you have a dataset with columns 'Date, Gender,
    /// Result' then you could pivot on Gender and the result set would have columns Date,Male,Female,Other,NumberOfResults' assuming your count SQL was called NumberOfResults.  Do not pick
    /// a column with thousands of unique values or you will end up with a very unwieldy result set that will probably crash the AggregateGraph when run.</para>
    /// 
    /// <para>One (DATE!) column can be marked as an Axis.  See AggregateContinuousDateAxisUI for description.</para>
    /// 
    /// </summary>
    public partial class AggregateEditor : AggregateEditor_Design,ISaveableUI
    {
        private IAggregateBuilderOptions _options;
        private AggregateConfiguration _aggregate;
        
        private List<TableInfo> _forcedJoins;

        IQuerySyntaxHelper _querySyntaxHelper;

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public Scintilla QueryHaving;

        //Constructor
        public AggregateEditor()
        {
            InitializeComponent();
            
            if(VisualStudioDesignMode)
                return;

            QueryHaving = new ScintillaTextEditorFactory().Create(new RDMPCommandFactory());
            
            gbHaving.Controls.Add(QueryHaving);

            QueryHaving.TextChanged += HavingTextChanged;
            aggregateContinuousDateAxisUI1.AxisSaved += ReloadUIFromDatabase;

            olvJoin.CheckStateGetter += ForceJoinCheckStateGetter;
            olvJoin.CheckStatePutter += ForceJoinCheckStatePutter;
            olvJoinTableName.ImageGetter += ImageGetter;

            olvJoin.AddDecoration(new EditingCellBorderDecoration { UseLightbox = true });
        }

        private object ImageGetter(object rowObject)
        {
            return _activator.CoreIconProvider.GetImage(rowObject);
        }
        
        private CheckState ForceJoinCheckStatePutter(object rowobject, CheckState newvalue)
        { 
            var ti = rowobject as TableInfo;
            var patientIndexTable = rowobject as JoinableCohortAggregateConfiguration;
            var patientIndexTableUse = rowobject as JoinableCohortAggregateConfigurationUse;

            var joiner = ((CatalogueRepository)_aggregate.Repository).AggregateForcedJoiner;
            

            //user is trying to use a joinable something
            if (newvalue == CheckState.Checked)
            {
                //user is trying to turn on usage of a TableInfo
                if(ti != null)
                {

                    joiner.CreateLinkBetween(_aggregate, ti);
                    _forcedJoins.Add(ti);
                    Publish();
                }

                if (patientIndexTable != null)
                {
                    var joinUse = patientIndexTable.AddUser(_aggregate);
                    olvJoin.RemoveObject(patientIndexTable);
                    olvJoin.AddObject(joinUse);
                    Publish();
                }
            }
            else
            {
                //user is trying to turn off usage of a TableInfo
                if (ti != null)
                {
                    joiner.BreakLinkBetween(_aggregate, ti);
                    _forcedJoins.Remove(ti);
                    _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(_aggregate));
                }

                if(patientIndexTableUse != null)
                {
                    var joinable = patientIndexTableUse.JoinableCohortAggregateConfiguration;

                    patientIndexTableUse.DeleteInDatabase();
                    olvJoin.RemoveObject(patientIndexTableUse);
                    olvJoin.AddObject(joinable);

                    Publish();
                }
            }

            return newvalue;

        }

        private void Publish()
        {
            _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(_aggregate));
        }

        private CheckState ForceJoinCheckStateGetter(object rowObject)
        {
            if (_forcedJoins == null)
                return CheckState.Indeterminate;

            if (rowObject is TableInfo)
                return _forcedJoins.Contains(rowObject)?CheckState.Checked : CheckState.Unchecked;

            if (rowObject is JoinableCohortAggregateConfiguration)
                return CheckState.Unchecked;

            if (rowObject is JoinableCohortAggregateConfigurationUse)
                return CheckState.Checked;
            
            return CheckState.Indeterminate;

        }

        public void SetAggregate(IActivateItems activator,AggregateConfiguration configuration, IAggregateBuilderOptions options = null)
        {
            _activator = activator;
            _aggregate = configuration;
            _options = options ?? new AggregateBuilderOptionsFactory().Create(configuration);
            
            ReloadUIFromDatabase();
        }

        private void ReloadUIFromDatabase()
        {
            isRefreshing = true;
            cbExtractable.Enabled = _options.ShouldBeEnabled(AggregateEditorSection.Extractable, _aggregate);
            cbExtractable.Checked = _aggregate.IsExtractable;

            gbPivot.Enabled = _options.ShouldBeEnabled(AggregateEditorSection.PIVOT, _aggregate);
            gbAxis.Enabled = _options.ShouldBeEnabled(AggregateEditorSection.AXIS, _aggregate);

            selectColumnUI1.SetUp(_activator, _options, _aggregate);
            
            tbID.Text = _aggregate.ID.ToString();

            SetNameText();

            tbDescription.Text = _aggregate.Description;

            DetermineFromTables();

            PopulateHavingText();

            var axisIfAny = _aggregate.GetAxisIfAny();
            var _axisDimensionIfAny = axisIfAny != null ? axisIfAny.AggregateDimension:null;
            var _pivotIfAny = _aggregate.PivotDimension;

            PopulatePivotDropdown(_axisDimensionIfAny,_pivotIfAny);

            PopulateAxis(_axisDimensionIfAny,_pivotIfAny);

            PopulateTopX();

            objectSaverButton1.SetupFor(_aggregate,_activator.RefreshBus);
            isRefreshing = false;
        }

        private void PopulateTopX()
        {
           _aggregateTopXui1.SetUp(_activator, _options, _aggregate);
        }

        private void DetermineFromTables()
        {
            //implicit use
            List<string> uniqueUsedTables = new List<string>();

            foreach (var d in _aggregate.AggregateDimensions)
            {
                var colInfo = d.ExtractionInformation.ColumnInfo;
                
                if (colInfo == null)
                    throw new Exception("Aggregate Configuration " + _aggregate + " (Catalogue '" +_aggregate.Catalogue+ "') has a Dimension '"+d+"' which is an orphan (someone deleted the ColumnInfo)");

                string toAdd = colInfo.TableInfo.ToString();

                if (!uniqueUsedTables.Contains(toAdd))
                    uniqueUsedTables.Add(toAdd);
            }

            lblFromTable.Text = string.Join(",", uniqueUsedTables);

            //explicit use
            olvJoin.ClearObjects();
            
            //explicit forced joins
            _forcedJoins = _aggregate.ForcedJoins.ToList();
            
            olvJoin.AddObjects(_forcedJoins);

            //available joinables
            var joinables = _options.GetAvailableJoinables(_aggregate);

            if(joinables != null)
                olvJoin.AddObjects(joinables);
            
            //and patient index tables too
            olvJoin.AddObjects(_aggregate.PatientIndexJoinablesUsed);
        }
        
        private void SetNameText()
        {
            if (_aggregate.IsJoinablePatientIndexTable())
                pictureBox1.Image = CatalogueIcons.BigPatientIndexTable;
            else if (_aggregate.IsCohortIdentificationAggregate)
                pictureBox1.Image = CatalogueIcons.BigCohort;
            else
                pictureBox1.Image = CatalogueIcons.BigGraph;

            //set the name to the tostring not the .Name so that we ignore the cic prefix
            tbName.Text = _aggregate.ToString();
        }

        private void OnListboxKeyUp(object sender, KeyEventArgs e)
        {
            var s = (ObjectListView) sender;
            
            if(e.KeyCode == Keys.Delete)
            {
                var deletable = s.SelectedObject as IDeleteable;

                if(deletable != null)
                    if(MessageBox.Show("Are you sure you want to delete '" + deletable +"'?","Confirm Delete",MessageBoxButtons.YesNo) == DialogResult.Yes)
                    {
                        try
                        {
                            deletable.DeleteInDatabase();
                        }
                        catch (Exception ex )
                        {
                            ExceptionViewer.Show(ex);
                        }
                        
                        ReloadUIFromDatabase();
                    }
            }
        }
 
        private bool isRefreshing;
       
        private void olvAny_CellEditFinishing(object sender, CellEditEventArgs e)
        {
            var revertable = e.RowObject as IRevertable;
            var countColumn = e.RowObject as AggregateCountColumn;

            e.Column.PutAspectByName(e.RowObject,e.NewValue);

            if (countColumn != null)
                _aggregate.CountSQL = countColumn.SelectSQL + (countColumn.Alias != null ? " as " + countColumn.Alias : "");
            else if (revertable != null)
            {
                if (revertable.HasLocalChanges().Evaluation == ChangeDescription.DatabaseCopyDifferent)
                    revertable.SaveToDatabase();
            }
            else
                throw new NotSupportedException("Why is user editing something that isn't IRevertable?");
        }
        
        #region Having
        private void HavingTextChanged(object sender, EventArgs e)
        {
            _aggregate.HavingSQL = QueryHaving.Text;
        }

        private void PopulateHavingText()
        {
            var autoComplete = new AutoCompleteProviderFactory(_activator).Create(_aggregate.GetQuerySyntaxHelper());
            autoComplete.RegisterForEvents(QueryHaving);
            autoComplete.Add(_aggregate);

            QueryHaving.Text = _aggregate.HavingSQL;
        }

        #endregion

        #region Pivot
        private void PopulatePivotDropdown(AggregateDimension axisIfAny, AggregateDimension pivotIfAny)
        {
            ddPivotDimension.Items.Clear();

            var dimensions = _aggregate.AggregateDimensions;

            //if theres an axis
            if (axisIfAny != null && !axisIfAny.Equals(pivotIfAny))//<- if this second thing is the case then the graph is totally messed up!
                dimensions = dimensions.Except(new[] {axisIfAny}).ToArray();//don't offer the axis as a pivot dimension!

            ddPivotDimension.Items.AddRange(dimensions);
            
            if(pivotIfAny != null)
                ddPivotDimension.SelectedItem = pivotIfAny;
        }
        private void ddPivotDimension_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(isRefreshing)
                return;

            var dimension = ddPivotDimension.SelectedItem as AggregateDimension;

            if (dimension != null && _aggregate != null)
            {
                EnsureCountHasAlias();
                EnsurePivotHasAlias(dimension);

                _aggregate.PivotOnDimensionID = dimension.ID;
                _aggregate.SaveToDatabase();
                _activator.RefreshBus.Publish(this,new RefreshObjectEventArgs(_aggregate));
            }

            ReloadUIFromDatabase();
        }

        private void EnsurePivotHasAlias(AggregateDimension dimension)
        {
            if (string.IsNullOrWhiteSpace(dimension.Alias))
            {
                dimension.Alias =  dimension.GetRuntimeName();
                dimension.SaveToDatabase();
            }
        }

        private void EnsureCountHasAlias()
        {
            string col;
            string alias;

            _querySyntaxHelper.SplitLineIntoSelectSQLAndAlias(_aggregate.CountSQL, out col, out alias);

            if (string.IsNullOrWhiteSpace(alias))
                _aggregate.CountSQL = col + _querySyntaxHelper.AliasPrefix+ " MyCount";
        }

        private void btnClearPivotDimension_Click(object sender, EventArgs e)
        {
            if (_aggregate != null)
            {
                _aggregate.PivotOnDimensionID = null;
                ddPivotDimension.SelectedItem = null;
            }
        }
        #endregion

        private void PopulateAxis(AggregateDimension axisIfAny, AggregateDimension pivotIfAny)
        {
            var allDimensions = _aggregate.AggregateDimensions.ToArray();
            
            //if theres a pivot then don't advertise that as an axis
            if (pivotIfAny != null && !pivotIfAny.Equals(axisIfAny))
                allDimensions = allDimensions.Except(new[] {pivotIfAny}).ToArray();
            
            ddAxisDimension.Items.Clear();
            ddAxisDimension.Items.AddRange(allDimensions);

            //should only be one
            var axisDimensions = allDimensions.Where(d => d.AggregateContinuousDateAxis != null).ToArray();

            if(axisDimensions.Length >1)
                if (
                    MessageBox.Show(
                        "Aggregate " + _aggregate +
                        " has more than 1 dimension, this is highly illegal, shall I delete all the axis configurations for you?",
                        "Delete all axis?", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    foreach (AggregateDimension a in axisDimensions)
                        a.AggregateContinuousDateAxis.DeleteInDatabase();
                else
                    return;

            if (axisIfAny == null)
            {
                aggregateContinuousDateAxisUI1.Dimension = null;
                return;
            }

            ddAxisDimension.SelectedItem = axisIfAny;
            aggregateContinuousDateAxisUI1.Dimension = axisIfAny;

        }

        private void ddAxisDimension_SelectedIndexChanged(object sender, EventArgs e)
        {
            if(isRefreshing)
                return;

            var selectedDimension = ddAxisDimension.SelectedItem as AggregateDimension;

            if(selectedDimension == null)
                return;
            
            //is there already an axis?
            var existing = _aggregate.GetAxisIfAny();

            //if they are selecting a different one
            if (existing != null && existing.AggregateDimension_ID != selectedDimension.ID)
                if (
                    MessageBox.Show(
                        "You are about to change the Axis dimension, are you sure you want to delete the old one '" +
                        existing.AggregateDimension + "' and replace it with '" + selectedDimension + "'?",
                        "Confirm deleting old Axis", MessageBoxButtons.YesNo) == DialogResult.Yes)
                    existing.DeleteInDatabase();
                else
                {
                    ReloadUIFromDatabase();//user chose to abandon the change
                    return;
                }

            var axis = new AggregateContinuousDateAxis(RepositoryLocator.CatalogueRepository, selectedDimension);
            axis.AxisIncrement = AxisIncrement.Month;
            axis.SaveToDatabase();
            ReloadUIFromDatabase();

        }

        private void btnClearAxis_Click(object sender, EventArgs e)
        {
            var existing = _aggregate.GetAxisIfAny();
            if(existing != null)
                existing.DeleteInDatabase();

            //also clear the pivot
            btnClearPivotDimension_Click(this,e);

            ReloadUIFromDatabase();
        }

        private void cbExtractable_CheckedChanged(object sender, EventArgs e)
        {
            if (isRefreshing)
                return;

            _aggregate.IsExtractable = cbExtractable.Checked;
            _aggregate.SaveToDatabase();
            _activator.RefreshBus.Publish(this, new RefreshObjectEventArgs(_aggregate));
        }

        private void tbDescription_TextChanged(object sender, EventArgs e)
        {
            if (_aggregate != null)
                _aggregate.Description = tbDescription.Text;
        }

        public override void SetDatabaseObject(IActivateItems activator, AggregateConfiguration databaseObject)
        {
            base.SetDatabaseObject(activator,databaseObject);

            _querySyntaxHelper = databaseObject.GetQuerySyntaxHelper();
            SetAggregate(activator, databaseObject);

            if (databaseObject.IsCohortIdentificationAggregate)
            {
                var cic = databaseObject.GetCohortIdentificationConfigurationIfAny();
                if (cic != null)
                    Add(new ExecuteCommandActivate(activator, cic), "Cohort Query");
            }
            else
                Add(new ExecuteCommandShow(activator, databaseObject.Catalogue, 0,true));

            Add(new ExecuteCommandViewSample(activator, databaseObject));
            Add(new ExecuteCommandExecuteAggregateGraph(activator,databaseObject));
            Add(new ExecuteCommandViewSqlParameters(activator, databaseObject));
        }

        public ObjectSaverButton GetObjectSaverButton()
        {
            return objectSaverButton1;
        }

        private void tbName_TextChanged(object sender, EventArgs e)
        {
            _aggregate.Name = tbName.Text;
            
            var cic = _aggregate.GetCohortIdentificationConfigurationIfAny();

            if (cic != null)
                cic.EnsureNamingConvention(_aggregate);
        }

        private void olvJoin_ItemActivate(object sender, EventArgs e)
        {
            var t = olvJoin.SelectedObject as TableInfo;
            if(t != null)
                _activator.RequestItemEmphasis(this,new EmphasiseRequest(t));
        }
    }
    
    [TypeDescriptionProvider(typeof(AbstractControlDescriptionProvider<AggregateEditor_Design, UserControl>))]
    public abstract class AggregateEditor_Design : RDMPSingleDatabaseObjectControl<AggregateConfiguration>
    {
    }
}
