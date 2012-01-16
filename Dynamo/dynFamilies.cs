//Copyright 2012 Ian Keough

//Licensed under the Apache License, Version 2.0 (the "License");
//you may not use this file except in compliance with the License.
//You may obtain a copy of the License at

//http://www.apache.org/licenses/LICENSE-2.0

//Unless required by applicable law or agreed to in writing, software
//distributed under the License is distributed on an "AS IS" BASIS,
//WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//See the License for the specific language governing permissions and
//limitations under the License.

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.IO;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Events;
using System.Reflection;
using System.Windows.Controls;
using System.Windows.Data;
using TextBox = System.Windows.Controls.TextBox;
using System.Windows.Forms;
using Dynamo.Controls;
using Dynamo.Connectors;
using Dynamo.Utilities;
using System.IO.Ports;

namespace Dynamo.Elements
{
    [ElementName("Family Type Selector")]
    [ElementDescription("An element which allows you to select a Family Type from a drop down list.")]
    [RequiresTransaction(true)]
    public class dynFamilyTypeSelector : dynElement, IDynamic
    {
        TextBox tb;
        System.Windows.Controls.ComboBox combo;
        Hashtable comboHash;
        FamilySymbol fs;

        public FamilySymbol SelectedFamilySymbol
        {
            get { return fs; }
        }

        public dynFamilyTypeSelector()
        {

            //widen the control
            this.topControl.Width = 300;

            //add a drop down list to the window
            combo = new System.Windows.Controls.ComboBox();
            combo.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            combo.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            this.inputGrid.Children.Add(combo);
            System.Windows.Controls.Grid.SetColumn(combo, 0);
            System.Windows.Controls.Grid.SetRow(combo, 0);

            combo.SelectionChanged += new SelectionChangedEventHandler(combo_SelectionChanged);
            combo.DropDownOpened += new EventHandler(combo_DropDownOpened);
            comboHash = new Hashtable();

            PopulateComboBox();

            OutPortData.Add(new PortData(null, "", "Family type.", typeof(dynFamilyTypeSelector)));
            base.RegisterInputsAndOutputs();
        }

        void combo_DropDownOpened(object sender, EventArgs e)
        {
            PopulateComboBox();
        }

        private void PopulateComboBox()
        {
            comboHash.Clear();
            combo.Items.Clear();

            //load all the currently loaded types into the combo list
            FilteredElementCollector fec = new FilteredElementCollector(dynElementSettings.SharedInstance.Doc.Document);
            fec.OfClass(typeof(Family));
            foreach (Family f in fec.ToElements())
            {
                foreach (FamilySymbol fs in f.Symbols)
                {
                    ComboBoxItem cbi = new ComboBoxItem();
                    string comboText = f.Name + ":" + fs.Name;
                    cbi.Content = comboText;
                    combo.Items.Add(cbi);
                    comboHash.Add(comboText, fs);
                }
            }
        }

        void combo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxItem cbi = combo.SelectedItem as ComboBoxItem;

            if (cbi != null)
            {
                fs = comboHash[cbi.Content] as FamilySymbol;
                OutPortData[0].Object = fs;

                OnDynElementReadyToBuild(EventArgs.Empty);
            }
        }

        public override void Update()
        {
            //tb.Text = OutPortData[0].Object.ToString();
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

    }

    [ElementName("Instance Parameter Mapper")]
    [ElementDescription("An element which maps the parameters of a Family Type.")]
    [RequiresTransaction(true)]
    public class dynInstanceParameterMapper : dynElement, IDynamic
    {
        Hashtable parameterMap = new Hashtable();

        public Hashtable ParameterMap
        {
            get { return parameterMap; }
        }

        public dynInstanceParameterMapper()
        {

            this.topControl.Width = 300;

            InPortData.Add(new PortData(null, "fi", "The family instance(s) to map.", typeof(dynElement)));
            OutPortData.Add(new PortData(null, "", "A map of parameter values on the instance.", typeof(dynInstanceParameterMapper)));
            OutPortData[0].Object = parameterMap;

            //add a button to the inputGrid on the dynElement
            System.Windows.Controls.Button paramMapButt = new System.Windows.Controls.Button();
            this.inputGrid.Children.Add(paramMapButt);
            paramMapButt.Margin = new System.Windows.Thickness(0, 0, 0, 0);
            paramMapButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            paramMapButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            paramMapButt.Click += new System.Windows.RoutedEventHandler(paramMapButt_Click);
            paramMapButt.Content = "Map";
            paramMapButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            paramMapButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            base.RegisterInputsAndOutputs();

        }

        void paramMapButt_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //read from the state objects
            //check the first port to make sure
            //it has a family tree to read from
            if (InPortData[0].Object != null)
            {
                CleanupOldPorts();

                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    //find the first family instance in the tree
                    //and map it
                    FamilyInstance fi = treeIn.Trunk.FindFirst() as FamilyInstance;
                    if (fi != null)
                    {
                        MapPorts(fi);
                    }

                }
            }
        }

        private void MapPorts(FamilyInstance fi)
        {
            parameterMap.Clear();

            foreach (Parameter p in fi.Parameters)
            {
                if (!p.IsReadOnly)  //don't want it if it is read only
                {
                    if (p.StorageType == StorageType.Double) //MDJ 11-23-11 - ian just had doubles but we need to add Ints, should convert to a switch case if we need more
                    {
                        string paramName = p.Definition.Name;

                        PortData pd = new PortData(null, 
                            p.Definition.Name.Substring(0,Math.Min(p.Definition.Name.Length,3)), 
                            paramName, 
                            typeof(dynDouble));
                        InPortData.Add(pd);
                        parameterMap.Add(paramName, pd.Object);
                    }
                    else if (p.StorageType == StorageType.Integer)
                    {
                        string paramName = p.Definition.Name;

                        PortData pd = new PortData(null,
                            p.Definition.Name.Substring(0, Math.Min(p.Definition.Name.Length, 3)),
                            paramName,
                            typeof(dynInt));
                        InPortData.Add(pd);
                        parameterMap.Add(paramName, pd.Object);
                    }
                    else if (p.StorageType == StorageType.String)
                    {
                        string paramName = p.Definition.Name;

                        PortData pd = new PortData(null,
                            p.Definition.Name.Substring(0, Math.Min(p.Definition.Name.Length, 3)),
                            paramName,
                            typeof(dynString));
                        InPortData.Add(pd);
                        parameterMap.Add(paramName, pd.Object);
                    }
                }
            }

            //add back new ports
            for (int i = 1; i < InPortData.Count; i++)
            {
                dynElement el = InPortData[i].Object as dynElement;

                RowDefinition rd = new RowDefinition();
                gridLeft.RowDefinitions.Add(rd);

                //add a port for each input
                //distribute the ports along the 
                //edges of the icon
                AddPort(el, PortType.INPUT, InPortData[i].NickName, i);
            }

            //resize this thing
            base.ResizeElementForInputs();

            base.SetToolTips();
        }

        private void CleanupOldPorts()
        {

            //clear all the inputs but the first one
            //which is the family instance
            //first kill all the connectors
            for (int i = 1; i < InPortData.Count; i++)
            {
                dynPort p = InPorts[i];

                //must remove the connectors iteratively
                //do not use a foreach here!
                while (p.Connectors.Count > 0)
                {
                    dynConnector c = p.Connectors[p.Connectors.Count - 1] as dynConnector;
                    c.Kill();
                }
            }

            //then remove all the ports
            while (InPorts.Count > 1)
            {
                InPorts.RemoveAt(InPorts.Count - 1);
                InPortData.RemoveAt(InPortData.Count - 1);
            }

            while (gridLeft.Children.Count > 2)
            {
                gridLeft.Children.RemoveAt(gridLeft.Children.Count - 1);
            }

            while (gridLeft.RowDefinitions.Count > 1)
            {
                //remove the port from the children list
                //gridLeft.Children.RemoveAt(gridLeft.RowDefinitions.Count - 1);
                gridLeft.RowDefinitions.RemoveAt(gridLeft.RowDefinitions.Count - 1);
            }

            this.UpdateLayout();
        }

        //sets the values on the family instances
        public override void Draw()
        {
            DataTree familyInstTree = InPortData[0].Object as DataTree;

            //skip the first port data because it's the family instances
            for (int i = 1; i < InPortData.Count; i++)
            {
                PortData pd = InPortData[i];
                //parameter value keys are the tooltip - the name 
                //of the parameter
                //set the objects on the parameter map
                parameterMap[pd.ToolTipString] = pd.Object;

                //start by assuming that you've got matching data trees
                DataTree doubleTree = pd.Object as DataTree;

                if (familyInstTree != null)
                {
                    if (doubleTree != null)
                    {
                        //get the parameter represented by the port data
                        Process(familyInstTree.Trunk, doubleTree.Trunk, pd.ToolTipString);
                    }
                    else
                    {
                        //if the incoming object is not null
                        //then let's try to use it.
                        if (pd.Object != null)
                        {
                            //we've got a single value incoming
                            ProcessSingleValue(familyInstTree.Trunk, pd.Object, pd.ToolTipString);
                        }
                    }
                }
            }

        }

        public void Process(DataTreeBranch familyBranch, DataTreeBranch doubleBranch, string paramName)
        {
            int leafCount = 0;
            foreach(object o in familyBranch.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    Parameter p = fi.get_Parameter(paramName);
                    if(p!= null)
                    {
                        if (p.StorageType == StorageType.Double) //MDJ 11-23-11 - ian just had doubles but we need to add Ints, should convert to a switch case if we need more
                        {
                            p.Set(Convert.ToDouble(doubleBranch.Leaves[leafCount]));
                        }
                        else if (p.StorageType == StorageType.Integer)
                        {
                            p.Set(Convert.ToInt32(doubleBranch.Leaves[leafCount]));
                        }
                        else if (p.StorageType == StorageType.String)
                        {
                            p.Set(Convert.ToString(doubleBranch.Leaves[leafCount]));
                        }
                    }
                }
                leafCount++;
            }

            int subBranchCount = 0;
            foreach (DataTreeBranch nextBranch in familyBranch.Branches)
            {
                //don't do this if the double tree doesn't
                //have a member in the same location
                if (doubleBranch.Branches.Count-1 >= subBranchCount)
                {
                    Process(nextBranch, doubleBranch.Branches[subBranchCount], paramName);
                }
                subBranchCount++;
            }
        }

        public void ProcessSingleValue(DataTreeBranch familyBranch, object value, string paramName)
        {
            int leafCount = 0;
            foreach (object o in familyBranch.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    Parameter p = fi.get_Parameter(paramName);
                    if (p != null)
                    {
                        if (p.StorageType == StorageType.Double) //MDJ 11-23-11 - ian just had doubles but we need to add Ints, should convert to a switch case if we need more
                        {
                            p.Set(Convert.ToDouble(value));
                        }
                        else if (p.StorageType == StorageType.Integer)
                        {
                            p.Set(Convert.ToInt32(value));
                        }
                        else if (p.StorageType == StorageType.String)
                        {
                            p.Set(Convert.ToString(value));
                        }
                    }
                }
                leafCount++;
            }

            foreach (DataTreeBranch nextBranch in familyBranch.Branches)
            {
                ProcessSingleValue(nextBranch, value, paramName);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }

    // MDJ added 11-14-11 
    // created new class dynFamilyInstanceCreatorXYZ by copying dynFamilyInstanceCreator, would be nice if there was a way to pass in either XYZ or RefPoints to previous class.
    // Hack: blind copy paste to change this to take XYZ instead
    // goal is to make family instances placeable in the project env
    // right now refpoints cannot be placed so the previous family instance creator fail
    //
    //

    [ElementName("Family Instance Creator by XYZ")]
    [ElementDescription("An element which allows you to create family instances from a set of XYZ coordinates.")]
    [RequiresTransaction(true)]
    public class dynFamilyInstanceCreatorXYZ : dynElement, IDynamic
    {

        public dynFamilyInstanceCreatorXYZ()
        {

            InPortData.Add(new PortData(null, "xyz", "xyz", typeof(dynXYZ)));
            InPortData.Add(new PortData(null, "typ", "The Family Symbol to use for instantiation.", typeof(dynFamilyTypeSelector)));

            //StatePortData.Add(new PortData(null, "map", "Instance parameter map.", typeof(dynInstanceParameterMapper)));

            OutPortData.Add(new PortData(null, "fi", "Family instances created by this operation.", typeof(dynFamilyInstanceCreator)));
            OutPortData[0].Object = this.Tree;

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    Process(treeIn.Trunk, this.Tree.Trunk);

                    //Hashtable parameterMap = StatePortData[0].Object as Hashtable;
                    //if(parameterMap != null)
                    //    ProcessState(this.Tree.Trunk, parameterMap);
                }
            }

            base.Draw();
        }

        public void Process(DataTreeBranch bIn, DataTreeBranch currentBranch)
        {

            foreach (object o in bIn.Leaves)
            {
               // ReferencePoint rp = o as ReferencePoint; //MDJ 11-14-11 
                XYZ pointXYZ = o as XYZ;

                if (pointXYZ != null)
                {
                    //get the location of the point
                    //XYZ pos = rp.Position;//MDJ 11-14-11 

                    try //MDJ 11-14-11
                    {
                        //MDJ 11-14-11 FamilyCreate vs Create (family vs project newfamilyinstance)
                        FamilySymbol fs = InPortData[1].Object as FamilySymbol;
                        if (dynElementSettings.SharedInstance.Doc.Document.IsFamilyDocument == true)  //Autodesk.Revit.DB.Document.IsFamilyDocument
                        {
                            FamilyInstance fi = dynElementSettings.SharedInstance.Doc.Document.FamilyCreate.NewFamilyInstance(pointXYZ, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);//MDJ 11-14-11 
                            Elements.Append(fi);
                            currentBranch.Leaves.Add(fi);
                        }
                        else
                        {
                            FamilyInstance fi = dynElementSettings.SharedInstance.Doc.Document.Create.NewFamilyInstance(pointXYZ, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);//MDJ 11-14-11 
                            Elements.Append(fi);
                            currentBranch.Leaves.Add(fi);
                        }



                    }
                    catch (Exception e)
                    {
                        TaskDialog.Show("Error", e.ToString());

                    } //MDJ 11-14-11

                    //Hashtable parameterMap = StatePortData[0].Object as Hashtable;
                    //if (parameterMap != null)
                    //{
                    //    foreach (DictionaryEntry de in parameterMap)
                    //    {
                    //        //find the parameter on the family instance
                    //        Parameter p = fi.Symbol.get_Parameter(de.Key.ToString());
                    //        if (p != null)
                    //        {
                    //            p.Set((double)de.Value);
                    //        }
                    //    }
                    //}
                }
            }

            foreach (DataTreeBranch b1 in bIn.Branches)
            {
                DataTreeBranch newBranch = new DataTreeBranch();
                this.Tree.Trunk.Branches.Add(newBranch);
                Process(b1, newBranch);
            }
        }

        public void ProcessState(DataTreeBranch bIn, Hashtable parameterMap)
        {
            foreach (object o in bIn.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    foreach (DictionaryEntry de in parameterMap)
                    {
                        if (de.Value != null)
                        {
                            //find the parameter on the family instance
                            Parameter p = fi.get_Parameter(de.Key.ToString());
                            if (p != null)
                            {
                                if (de.Value != null)
                                    p.Set((double)de.Value);
                            }
                        }
                    }
                }
            }

            foreach (DataTreeBranch nextBranch in bIn.Branches)
            {
                ProcessState(nextBranch, parameterMap);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }

    // MDJ added 11-21-11 
    // created new class dynFamilyInstanceCreatorBySelection by copying dynFamilyInstanceCreator, 
    //
    //

    [ElementName("Family Instance Creator by Selection")]
    [ElementDescription("An element which allows you to select a family instancs from the document and reference it in Dynamo.")]
    [RequiresTransaction(true)]
    public class dynFamilyInstanceCreatorSelection : dynElement, IDynamic, INotifyPropertyChanged
    {

        public dynFamilyInstanceCreatorSelection()
        {

            //InPortData.Add(new PortData(null, "xyz", "xyz", typeof(dynXYZ)));
            //InPortData.Add(new PortData(null, "typ", "The Family Symbol to use for instantiation.", typeof(dynFamilyTypeSelector)));
            
            //StatePortData.Add(new PortData(null, "map", "Instance parameter map.", typeof(dynInstanceParameterMapper)));

            OutPortData.Add(new PortData(null, "fi", "Family instances created by this operation.", typeof(dynFamilyInstanceCreator)));
            OutPortData[0].Object = this.Tree;

            //add a button to the inputGrid on the dynElement
            System.Windows.Controls.Button familyInstanceButt = new System.Windows.Controls.Button();
            this.inputGrid.Children.Add(familyInstanceButt);
            familyInstanceButt.Margin = new System.Windows.Thickness(0, 0, 0, 0);
            familyInstanceButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
            familyInstanceButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;
            familyInstanceButt.Click += new System.Windows.RoutedEventHandler(familyInstanceButt_Click);
            familyInstanceButt.Content = "Select Inst.";
            familyInstanceButt.HorizontalAlignment = System.Windows.HorizontalAlignment.Stretch;
            familyInstanceButt.VerticalAlignment = System.Windows.VerticalAlignment.Center;

            base.RegisterInputsAndOutputs();

        }
        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(String info)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(info));
            }
        }

        public FamilyInstance pickedFamilyInstance;

        public FamilyInstance PickedFamilyInstance
        {
            get { return pickedFamilyInstance; }
            set
            {
                pickedFamilyInstance = value;
                NotifyPropertyChanged("PickedFamilyInstance");
            }
        }

        private ElementId familyInstanceID;

        private ElementId FamilyInstanceID
        {
            get { return familyInstanceID; }
            set
            {
                familyInstanceID = value;
                NotifyPropertyChanged("FamilyInstanceID");
            }
        }
        void familyInstanceButt_Click(object sender, System.Windows.RoutedEventArgs e)
        {
            //read from the state objects
            //if (CheckInputs())
            //{
            PickedFamilyInstance = Dynamo.Utilities.SelectionHelper.RequestFamilyInstanceSelection(dynElementSettings.SharedInstance.Doc, "Selec Massing Family Instance", dynElementSettings.SharedInstance);

            if (PickedFamilyInstance != null)
                {
                    FamilyInstanceID = PickedFamilyInstance.Id;
                    Process(); // don't need to pass in anything because family instances and tree already have accesors.
                }
            //}
        }
        

        public override void Draw()
        {

                // watch?
                //currentBranch.Leaves.Add(fi);

            Process(); // don't need to pass in anything because family instances and tree already have accesors.
            base.Draw();
        }

        public void Process()
        {
            if (PickedFamilyInstance != null)
            {
                if (PickedFamilyInstance.Id.IntegerValue == FamilyInstanceID.IntegerValue) // sanity check
                {
                    //need to put a watcher on this to ensure deletion works 
                    this.Tree.Clear(); // clean out old refs
                    this.Tree.Trunk.Branches.Add(new DataTreeBranch());
                    this.Tree.Trunk.Branches[0].Leaves.Add(PickedFamilyInstance);
                    OutPortData[0].Object = this.Tree;
                }

            }


        }

       
        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            //base.Destroy();
        }
    }


    [ElementName("Family Instance Creator")]
    [ElementDescription("An element which allows you to create family instances from a set of points.")]
    [RequiresTransaction(true)]
    public class dynFamilyInstanceCreator : dynElement, IDynamic
    {

        public dynFamilyInstanceCreator()
        {

            InPortData.Add(new PortData(null, "pt", "Reference points.", typeof(dynReferencePoint)));
            InPortData.Add(new PortData(null, "typ", "The Family Symbol to use for instantiation.", typeof(dynFamilyTypeSelector)));

            //StatePortData.Add(new PortData(null, "map", "Instance parameter map.", typeof(dynInstanceParameterMapper)));

            OutPortData.Add(new PortData(null, "fi", "Family instances created by this operation.", typeof(dynFamilyInstanceCreator)));
            OutPortData[0].Object = this.Tree;

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    Process(treeIn.Trunk, this.Tree.Trunk);
                }
            }

            base.Draw();
        }

        public void Process(DataTreeBranch bIn, DataTreeBranch currentBranch)
        {

            foreach (object o in bIn.Leaves)
            {
                ReferencePoint rp = o as ReferencePoint;

                if (rp != null)
                {
                    //get the location of the point
                    XYZ pos = rp.Position;
                    FamilySymbol fs = InPortData[1].Object as FamilySymbol;
                    FamilyInstance fi = dynElementSettings.SharedInstance.Doc.Document.FamilyCreate.NewFamilyInstance(pos, fs, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);

                    Elements.Append(fi);
                    currentBranch.Leaves.Add(fi);

                }
            }

            foreach (DataTreeBranch b1 in bIn.Branches)
            {
                DataTreeBranch newBranch = new DataTreeBranch();
                this.Tree.Trunk.Branches.Add(newBranch);
                Process(b1, newBranch);
            }
        }

        public void ProcessState(DataTreeBranch bIn, Hashtable parameterMap)
        {
            foreach (object o in bIn.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    foreach (DictionaryEntry de in parameterMap)
                    {
                        if (de.Value != null)
                        {
                            //find the parameter on the family instance
                            Parameter p = fi.get_Parameter(de.Key.ToString());
                            if (p != null)
                            {
                                if (de.Value != null)
                                    p.Set((double)de.Value);
                            }
                        }
                    }
                }
            }

            foreach (DataTreeBranch nextBranch in bIn.Branches)
            {
                ProcessState(nextBranch, parameterMap);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }

    [ElementName("Family Instance Parameter Evaluation")]
    [ElementDescription("An element which allows you to modify parameters on family instances.")]
    [RequiresTransaction(true)]
    public class dynFamilyInstanceParameterEvaluation: dynElement, IDynamic
    {

        public dynFamilyInstanceParameterEvaluation()
        {

            InPortData.Add(new PortData(null, "fi", "Family instances.", typeof(dynElement)));
            InPortData.Add(new PortData(null, "map", "Parameter map.", typeof(dynInstanceParameterMapper)));

            base.RegisterInputsAndOutputs();

        }

        public override void Draw()
        {
            if (CheckInputs())
            {
                DataTree treeIn = InPortData[0].Object as DataTree;
                if (treeIn != null)
                {
                    Hashtable parameterMap = InPortData[1].Object as Hashtable;
                    if (parameterMap != null)
                        ProcessState(treeIn.Trunk, parameterMap);
                }
            }

            base.Draw();
        }

        private void ProcessState(DataTreeBranch bIn, Hashtable parameterMap)
        {
            foreach (object o in bIn.Leaves)
            {
                FamilyInstance fi = o as FamilyInstance;
                if (fi != null)
                {
                    foreach (DictionaryEntry de in parameterMap)
                    {
                        if (de.Value != null)
                        {
                            //find the parameter on the family instance
                            Parameter p = fi.get_Parameter(de.Key.ToString());
                            if (p != null)
                            {
                                if (de.Value != null)

                                    if (p.StorageType == StorageType.Double)
                                    {
                                        p.Set((double)de.Value);
                                    }
                                    else if (p.StorageType == StorageType.Integer)
                                    {
                                        p.Set((int)de.Value);
                                    }
                                    else if (p.StorageType == StorageType.String)
                                    {
                                        p.Set((string)de.Value);
                                    }
                            }
                        }
                    }
                }
            }

            foreach (DataTreeBranch nextBranch in bIn.Branches)
            {
                ProcessState(nextBranch, parameterMap);
            }
        }

        public override void Update()
        {
            OnDynElementReadyToBuild(EventArgs.Empty);
        }

        public override void Destroy()
        {
            base.Destroy();
        }
    }
}

