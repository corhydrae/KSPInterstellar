﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reflection;
using UnityEngine;

namespace FNPlugin.Storage
{
    public class FSresource
    {
        //public PartResource resource;
        public string name;
        public int ID;
        public float ratio;
        public double currentSupply = 0f;
        public double amount = 0f;
        public double maxAmount = 0f;

        public FSresource(string _name, float _ratio)
        {
            name = _name;
            ID = _name.GetHashCode();
            ratio = _ratio;
        }

        public FSresource(string _name)
        {
            name = _name;
            ID = _name.GetHashCode();
            ratio = 1f;
        }
    }

    public class FSmodularTank
    {
        public List<FSresource> resources = new List<FSresource>();
    }

    public class InsterstellarFuelSwitch : PartModule, IPartCostModifier
    {
        [KSPField]
        public string resourceNames = "ElectricCharge;LiquidFuel,Oxidizer;MonoPropellant";
        [KSPField]
        public string resourceAmounts = "100;75,25;200";
        [KSPField]
        public string initialResourceAmounts = "";
        [KSPField]
        public float basePartMass = 0.25f;
        [KSPField]
        public string tankMass = "0;0;0;0";
        [KSPField]
        public string tankCost = "0; 0; 0; 0";
        [KSPField]
        public bool displayCurrentTankCost = false;
        [KSPField]
        public bool hasGUI = true;
        [KSPField]
        public bool availableInFlight = false;
        [KSPField]
        public bool availableInEditor = true;
        [KSPField]
        public bool showInfo = true; // if false, does not feed info to the part list pop up info menu
        [KSPField(guiActive = false, guiActiveEditor = false, guiName = "Added cost")]
        public float addedCost = 0f;
        [KSPField(guiActive = false, guiActiveEditor = true, guiName = "Dry mass")]
        public float dryMassInfo = 0f;
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Volume Multiplier")]
        public float volumeMultiplier = 1f;
        [KSPField(isPersistant = false, guiActiveEditor = true, guiName = "Mass Multiplier")]
        public float massMultiplier = 1f;

        // Persistants
        [KSPField(isPersistant = true)]
        public int selectedTankSetup = -1;
        [KSPField(isPersistant = true)]
        public bool hasLaunched = false;
        [KSPField(isPersistant = true)]
        public bool gameLoaded = false;
        [KSPField(isPersistant = true)]
        public bool configLoaded = false;

        private List<FSmodularTank> tankList;
        private List<double> weightList;
        private List<double> tankCostList;
        private bool initialized = false;

        UIPartActionWindow tweakableUI;

        public override void OnStart(PartModule.StartState state)
        {
            Debug.Log("InsterstellarFuelSwitch OnStart loaded persistant selectedTankSetup = " + selectedTankSetup);
            initializeData();

            if (selectedTankSetup == -1)
                selectedTankSetup = 0;

            if (state != StartState.Editor)
            {
                Debug.Log("InsterstellarFuelSwitch OnStart started outside editor");

                assignResourcesToPart(false, gameLoaded);
                gameLoaded = true;
            }
            else
            {
                Debug.Log("InsterstellarFuelSwitch OnStart started inside editor");
                assignResourcesToPart(false, false);
            }
        }
        public override void OnAwake()
        {
            if (configLoaded)
                initializeData();
        }
        public override void OnLoad(ConfigNode node)
        {
            base.OnLoad(node);
            if (!configLoaded)
                initializeData();

            configLoaded = true;
        }

        public static List<double> parseDoubles(string stringOfDoubles)
        {
            System.Collections.Generic.List<double> list = new System.Collections.Generic.List<double>();
            string[] array = stringOfDoubles.Trim().Split(';');
            for (int i = 0; i < array.Length; i++)
            {
                double item = 0f;
                if (double.TryParse(array[i].Trim(), out item))
                    list.Add(item);
                else
                    Debug.Log("InsterstellarFuelSwitch parseDoubles: invalid float: [len:" + array[i].Length + "] '" + array[i] + "']");
            }
            return list;
        }
        private void initializeData()
        {
            if (!initialized)
            {
                setupTankList(false);
                weightList = parseDoubles(tankMass);
                tankCostList = parseDoubles(tankCost);

                if (HighLogic.LoadedSceneIsFlight) 
                    hasLaunched = true;

                if (hasGUI)
                {
                    Events["nextTankSetupEvent"].guiActive = availableInFlight;
                    Events["nextTankSetupEvent"].guiActiveEditor = availableInEditor;
                    Events["previousTankSetupEvent"].guiActive = availableInFlight;
                    Events["previousTankSetupEvent"].guiActiveEditor = availableInEditor;
                }
                else
                {
                    Events["nextTankSetupEvent"].guiActive = false;
                    Events["nextTankSetupEvent"].guiActiveEditor = false;
                    Events["previousTankSetupEvent"].guiActive = false;
                    Events["previousTankSetupEvent"].guiActiveEditor = false;
                }

                if (HighLogic.CurrentGame == null || HighLogic.CurrentGame.Mode == Game.Modes.CAREER)
                    Fields["addedCost"].guiActiveEditor = displayCurrentTankCost;

                initialized = true;
            }
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Next tank setup")]
        public void nextTankSetupEvent()
        {
            selectedTankSetup++;
            Debug.Log("InsterstellarFuelSwitch nextTankSetupEvent selectedTankSetup++ = " + selectedTankSetup);

            if (selectedTankSetup >= tankList.Count)
            {
                selectedTankSetup = 0;
                Debug.Log("InsterstellarFuelSwitch nextTankSetupEvent selectedTankSetup = 0 ");
            }

            assignResourcesToPart(true);
        }

        [KSPEvent(guiActive = true, guiActiveEditor = true, guiName = "Previous tank setup")]
        public void previousTankSetupEvent()
        {
            selectedTankSetup--;
            Debug.Log("InsterstellarFuelSwitch previousTankSetupEvent selectedTankSetup-- = " + selectedTankSetup);

            if (selectedTankSetup < 0)
            {
                selectedTankSetup = tankList.Count - 1;
                Debug.Log("InsterstellarFuelSwitch previousTankSetupEvent tankList.Count - 1 = " + selectedTankSetup);
            }

            assignResourcesToPart(true);
        }
        public void selectTankSetup(int i, bool calledByPlayer)
        {
            initializeData();
            if (selectedTankSetup != i)
            {
                selectedTankSetup = i;
                Debug.Log("InsterstellarFuelSwitch selectTankSetup selectedTankSetup = i = " + selectedTankSetup);
                assignResourcesToPart(calledByPlayer);
            }
        }

        private void assignResourcesToPart(bool calledByPlayer, bool calledAtStartup = false)
        {
            // destroying a resource messes up the gui in editor, but not in flight.
            setupTankInPart(part, calledByPlayer, calledAtStartup);
            if (HighLogic.LoadedSceneIsEditor)
            {
                for (int s = 0; s < part.symmetryCounterparts.Count; s++)
                {
                    setupTankInPart(part.symmetryCounterparts[s], calledByPlayer);
                    InsterstellarFuelSwitch symSwitch = part.symmetryCounterparts[s].GetComponent<InsterstellarFuelSwitch>();
                    if (symSwitch != null)
                    {
                        symSwitch.selectedTankSetup = selectedTankSetup;
                        Debug.Log("InsterstellarFuelSwitch assignResourcesToPart symSwitch.selectedTankSetup = selectedTankSetup = " + selectedTankSetup);
                    }
                }
            }
            //Debug.Log("refreshing UI");
            if (tweakableUI == null)
                tweakableUI = part.FindActionWindow();

            if (tweakableUI != null)
                tweakableUI.displayDirty = true;
            else
                Debug.Log("InsterstellarFuelSwitch assignResourcesToPart - no UI to refresh");
        }
        private void setupTankInPart(Part currentPart, bool calledByPlayer, bool calledAtStartup = false)
        {
            // create new ResourceNode
            List<string> newResources = new List<string>();
            List<ConfigNode> newResourceNodes = new List<ConfigNode>(); 
            for (int tankCount = 0; tankCount < tankList.Count; tankCount++)
            {
                if (selectedTankSetup != tankCount) continue;

                Debug.Log("InsterstellarFuelSwitch assignResourcesToPart setupTankInPart = " + selectedTankSetup);
                for (int resourceCount = 0; resourceCount < tankList[tankCount].resources.Count; resourceCount++)
                {
                    if (tankList[tankCount].resources[resourceCount].name == "Structural") continue;

                    var resourceName = tankList[tankCount].resources[resourceCount].name;
                    newResources.Add(resourceName);

                    ConfigNode newResourceNode = new ConfigNode("RESOURCE");
                    newResourceNode.AddValue("name", resourceName);
                    newResourceNode.AddValue("maxAmount", tankList[tankCount].resources[resourceCount].maxAmount * volumeMultiplier);

                    if (calledByPlayer && !HighLogic.LoadedSceneIsEditor)
                        newResourceNode.AddValue("amount", 0.0f);
                    else
                        newResourceNode.AddValue("amount", tankList[tankCount].resources[resourceCount].amount * volumeMultiplier);

                    newResourceNodes.Add(newResourceNode);
                }
            }

            // verify we need to update
            if (newResourceNodes.Count > 0) 
            {
                currentPart.Resources.list.Clear();
                PartResource[] partResources = currentPart.GetComponents<PartResource>();
                for (int i = 0; i < partResources.Length; i++)
                {
                    var resource = partResources[i];
                    // only remove resources that are not in our newResources list
                    if (!newResources.Any(r => r.Equals(resource.resourceName)))
                    {
                        Debug.Log("InsterstellarFuelSwitch setupTankInPart removing resource: " + resource.resourceName);
                        DestroyImmediate(resource);
                    }
                    else // We don't want to add duplicate resources.
                        newResourceNodes.RemoveAll(r => r.GetValue("name").Equals(resource.resourceName)); // There must be a more efficient way to do this by pairing the node with the name.
                }

                Debug.Log("InsterstellarFuelSwitch setupTankInPart adding new resources: " + Print(newResources));
                foreach (var resoureNode in newResourceNodes)
                {
                    
                    currentPart.AddResource(resoureNode);
                }

                currentPart.Resources.UpdateList();
                updateWeight(currentPart, selectedTankSetup, calledByPlayer);
                updateCost();
            }
            else
                Debug.Log("InsterstellarFuelSwitch setupTankInPart keeps existing resources unchanged");
              
             //*/
        }

        public static string Print( IList<string> list)
        {
            string result = "";
            foreach(var item in list)
            {
                result += item + ";";
            }
            return result;
        }

        public static bool ListEquals<T>(IList<T> list1, IList<T> list2)
        {
            if (list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (!list1[i].Equals(list2[i])) return false;
            }
            return true;
        }

        private float updateCost()
        {
            //GameEvents.onEditorShipModified.Fire(EditorLogic.fetch.ship); //crashes game
            return selectedTankSetup >= 0 && selectedTankSetup < tankCostList.Count ? (float)tankCostList[selectedTankSetup] : 0f;
        }
        private void updateWeight(Part currentPart, int newTankSetup, bool calledByPlayer = false)
        {
            // when changed by player
            if (calledByPlayer && HighLogic.LoadedSceneIsFlight) return;

            if (newTankSetup < weightList.Count)
                currentPart.mass = (float)((basePartMass + weightList[newTankSetup]) * massMultiplier);
        }
        public override void OnUpdate()
        {
            //There were some issues with resources slowly trickling in, so I changed this to 0.1% instead of empty.
            bool showSwitchButtons = availableInFlight && !part.GetComponents<PartResource>().Any(r => r.amount > r.maxAmount / 1000);

            Events["nextTankSetupEvent"].guiActive = showSwitchButtons;
            Events["previousTankSetupEvent"].guiActive = showSwitchButtons;
        }
        public void Update()
        {
            if (HighLogic.LoadedSceneIsEditor)
                dryMassInfo = part.mass;
        }
        private void setupTankList(bool calledByPlayer)
        {
            tankList = new List<FSmodularTank>();
            weightList = new List<double>();
            tankCostList = new List<double>();

            // First find the amounts each tank type is filled with
            List<List<double>> resourceList = new List<List<double>>();
            List<List<double>> initialResourceList = new List<List<double>>();
            string[] resourceTankArray = resourceAmounts.Split(';');
            string[] initialResourceTankArray = initialResourceAmounts.Split(';');

            if (initialResourceAmounts.Equals("") || initialResourceTankArray.Length != resourceTankArray.Length)
                initialResourceTankArray = resourceTankArray;

            //Debug.Log("FSDEBUGRES: " + resourceTankArray.Length+" "+resourceAmounts);
            for (int tankCount = 0; tankCount < resourceTankArray.Length; tankCount++)
            {
                resourceList.Add(new List<double>());
                initialResourceList.Add(new List<double>());
                string[] resourceAmountArray = resourceTankArray[tankCount].Trim().Split(',');
                string[] initialResourceAmountArray = initialResourceTankArray[tankCount].Trim().Split(',');

                if (initialResourceAmounts.Equals("") || initialResourceAmountArray.Length != resourceAmountArray.Length)
                    initialResourceAmountArray = resourceAmountArray;

                for (int amountCount = 0; amountCount < resourceAmountArray.Length; amountCount++)
                {
                    try
                    {
                        resourceList[tankCount].Add(double.Parse(resourceAmountArray[amountCount].Trim()));
                        initialResourceList[tankCount].Add(double.Parse(initialResourceAmountArray[amountCount].Trim()));
                    }
                    catch
                    {
                        Debug.Log("InsterstellarFuelSwitch: error parsing resource amount " + tankCount + "/" + amountCount + ": '" + resourceTankArray[amountCount] + "': '" + resourceAmountArray[amountCount].Trim() + "'");
                    }
                }
            }

            // Then find the kinds of resources each tank holds, and fill them with the amounts found previously, or the amount hey held last (values kept in save persistence/craft)
            string[] tankArray = resourceNames.Split(';');
            for (int tankCount = 0; tankCount < tankArray.Length; tankCount++)
            {
                FSmodularTank newTank = new FSmodularTank();
                tankList.Add(newTank);
                string[] resourceNameArray = tankArray[tankCount].Split(',');
                for (int nameCount = 0; nameCount < resourceNameArray.Length; nameCount++)
                {
                    FSresource newResource = new FSresource(resourceNameArray[nameCount].Trim(' '));
                    if (resourceList[tankCount] != null)
                    {
                        if (nameCount < resourceList[tankCount].Count)
                        {
                            newResource.maxAmount = resourceList[tankCount][nameCount];
                            newResource.amount = initialResourceList[tankCount][nameCount];
                        }
                    }
                    newTank.resources.Add(newResource);
                }
            }
        }

        public float GetModuleCost()
        {
            return updateCost();
        }

        public float GetModuleCost(float modifier)
        {
            return updateCost();
        }

        public static List<string> parseNames(string names)
        {
            return parseNames(names, false, true, string.Empty);
        }

        public static List<string> parseNames(string names, bool replaceBackslashErrors)
        {
            return parseNames(names, replaceBackslashErrors, true, string.Empty);
        }

        public static List<string> parseNames(string names, bool replaceBackslashErrors, bool trimWhiteSpace, string prefix)
        {
            List<string> source = names.Split(';').ToList<string>();
            for (int i = source.Count - 1; i >= 0; i--)
            {
                if (source[i] == string.Empty)
                    source.RemoveAt(i);
            }
            if (trimWhiteSpace)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    source[i] = source[i].Trim(' ');
                }
            }
            if (prefix != string.Empty)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    source[i] = prefix + source[i];
                }
            }
            if (replaceBackslashErrors)
            {
                for (int i = 0; i < source.Count; i++)
                {
                    source[i] = source[i].Replace('\\', '/');
                }
            }
            return source.ToList<string>();
        }

        public override string GetInfo()
        {
            if (showInfo)
            {
                List<string> resourceList = parseNames(resourceNames);
                StringBuilder info = new StringBuilder();
                info.AppendLine("Fuel tank setups available:");
                for (int i = 0; i < resourceList.Count; i++)
                {
                    info.AppendLine(resourceList[i].Replace(",", ", "));
                }
                return info.ToString();
            }
            else
                return string.Empty;
        }
    } 
}