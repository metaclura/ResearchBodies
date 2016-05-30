﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RSTUtils;
using UnityEngine;

namespace ResearchBodies
{
    [KSPAddon(KSPAddon.Startup.MainMenu, true)]
    public class Database : MonoBehaviour
    {
        //This is a deprecated dictionary that stores Priority #'s against the CelestialBodies. Loaded from PRIORITIES node in database.cfg
        public static Dictionary<CelestialBody, int> Priority = new Dictionary<CelestialBody, int>();
        //This is a dictionary of the Discovery Messages for the CelestialBodies. Loaded from ONDISCOVERY node in database.cfg
        public static Dictionary<string, string> DiscoveryMessage = new Dictionary<string, string>();
        //This is a dictionary of the Ignore Levels for each CelestialBody for each difficulty level. Loaded from IGNORELEVELS node in database.cfg.
        public static Dictionary<CelestialBody, BodyIgnoreData> IgnoreData = new Dictionary<CelestialBody, BodyIgnoreData>();
        //This is a temporary dictionary until I can re-write the storage solution for this mod. Being used now for the Kopernicus Barycenter info and related
        //celestial body only.
        public static Dictionary<CelestialBody, CelestialBodyInfo> CelestialBodies = new Dictionary<CelestialBody, CelestialBodyInfo>();

        public static Texture2D IconTexture;
        public static List<CelestialBody> IgnoreBodies = new List<CelestialBody>();
        //This is a list of Nothing to See here strings loaded from NOTHING node in database.cfg
        public static List<string> NothingHere = new List<string>();
        // public static Dictionary<CelestialBody, Texture2D> BlurredTextures = new Dictionary<CelestialBody, Texture2D>();
        public static int chances;
        public static int[] StartResearchCosts, ProgressResearchCosts, ScienceRewards;
        public static bool enableInSandbox, allowTSlevel1 = false;
        internal static bool UseAppLauncher = true;

        /// <summary>
        /// Tarsier Space Tech Interface fields
        /// </summary>
        internal bool isTSTInstalled = false;
        internal static List<CelestialBody> TSTCBGalaxies = new List<CelestialBody>();
        public static List<CelestialBody> BodyList = new List<CelestialBody>();
        

        //This is only called by the Startup Menu GUI to show ignored bodies based on the level passed in. 
        public static string GetIgnoredBodies(Level l) 
        {
            string _bodies = Locales.currentLocale.Values["start_availableBodies"] + " : ";
            foreach (CelestialBody body in BodyList.Where(b => IgnoreData[b].GetLevel(l)))
            {
                _bodies += body.GetName() + ", ";
            }
            return _bodies;
        }

        void Start()
        {
            isTSTInstalled = Utilities.IsTSTInstalled;
            if (isTSTInstalled)  //If TST assembly is present, initialise TST wrapper.
            {
                if (!TSTWrapper.InitTSTWrapper())
                {
                    isTSTInstalled = false; //If the initialise of wrapper failed set bool to false, we won't be interfacing to TST today.
                }
            }

            Textures.LoadIconAssets();

            //Get a list of CBs
            BodyList = FlightGlobals.Bodies.ToList(); 
            if (isTSTInstalled && TSTWrapper.APITSTReady) //If TST is installed add the TST Galaxies to the list.
            {
                BodyList = BodyList.Concat(TSTWrapper.actualTSTAPI.CBGalaxies).ToList();
            }
            
            IconTexture = GameDatabase.Instance.GetTexture("ResearchBodies/Icons/icon", false);

            //Load the database.cfg file.
            //===========================
            ConfigNode cfg = ConfigNode.Load("GameData/ResearchBodies/database.cfg");
            string[] sep = new string[] { " " };

            //Get Costs
            string[] _startResearchCosts;
            _startResearchCosts = cfg.GetNode("RESEARCHBODIES").GetValue("StartResearchCosts").Split(sep, StringSplitOptions.RemoveEmptyEntries);
            StartResearchCosts = new int[] { int.Parse(_startResearchCosts[0]), int.Parse(_startResearchCosts[1]), int.Parse(_startResearchCosts[2]), int.Parse(_startResearchCosts[3]) };

            string[] _progressResearchCosts;
            _progressResearchCosts = cfg.GetNode("RESEARCHBODIES").GetValue("ProgressResearchCosts").Split(sep, StringSplitOptions.RemoveEmptyEntries);
            ProgressResearchCosts = new int[] { int.Parse(_progressResearchCosts[0]), int.Parse(_progressResearchCosts[1]), int.Parse(_progressResearchCosts[2]), int.Parse(_progressResearchCosts[3]) };

            string[] _scienceRewards;
            _scienceRewards = cfg.GetNode("RESEARCHBODIES").GetValue("ScienceRewards").Split(sep, StringSplitOptions.RemoveEmptyEntries);
            ScienceRewards = new int[] { int.Parse(_scienceRewards[0]), int.Parse(_scienceRewards[1]), int.Parse(_scienceRewards[2]), int.Parse(_scienceRewards[3]) };



            RSTLogWriter.Log_Debug("Loading Priority database");
            foreach (CelestialBody body in BodyList)
            {
                //Load the priorities - DEPRECATED
                string name = body.GetName();
                foreach (ConfigNode.Value value in cfg.GetNode("RESEARCHBODIES").GetNode("PRIORITIES").values)
                {
                    if (name == value.name)
                    {
                        Priority[body] = int.Parse(value.value);
                        RSTLogWriter.Log_Debug("Priority for body {0} set to {1}.", name , value.value);
                    }
                }
                //Load the ondiscovery values - English only, which then get over-written in Locale.cs
                foreach (ConfigNode.Value value in cfg.GetNode("RESEARCHBODIES").GetNode("ONDISCOVERY").values)
                {
                    if (value.name == name)
                        DiscoveryMessage[value.name] = value.value;
                }
                //IF current body is not in discovery message dictionary we add it with default string
                if (!DiscoveryMessage.ContainsKey(body.GetName()))
                    DiscoveryMessage[body.GetName()] = "Now tracking " + name + " !";

                //This WOULD load the blurredTextures that we want to implement.
                // if (cfg.GetNode("RESEARCHBODIES").HasValue("blurredTextures"))
                // {
                //    foreach (string str in Directory.GetFiles(cfg.GetNode("RESEARCHBODIES").GetValue("blurredTextures")))
                //    {
                //        if (str.Contains(body.GetName()))
                //            BlurredTextures[body] = GameDatabase.Instance.GetTexture(cfg.GetNode("RESEARCHBODIES").GetValue("blurredTextures").Replace("GameData/", "") + "/" + body.GetName(), false);
                //    }
                // }
            }

            //Load the IgnoreData dictionary.
            RSTLogWriter.Log_Debug("Loading ignore body list from database");
            foreach (ConfigNode.Value value in cfg.GetNode("RESEARCHBODIES").GetNode("IGNORELEVELS").values)
            {
                foreach (CelestialBody body in BodyList)
                {
                    if (body.GetName() == value.name)
                    {
                        BodyIgnoreData ignoredata;
                        string[] args;
                        args = value.value.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                        if (body.Radius < 100) //Set Kopernicus barycenter and binaries to ignore.
                        {
                            ignoredata = new BodyIgnoreData(true, true, true, true);
                        }
                        else
                        {
                            ignoredata = new BodyIgnoreData(bool.Parse(args[0]), bool.Parse(args[1]), bool.Parse(args[2]), bool.Parse(args[3]));   
                        }
                        IgnoreData[body] = ignoredata;
                        RSTLogWriter.Log_Debug("Body Ignore Data for {0} : {1}" , body.GetName() , IgnoreData[body]);
                    }
                }
            }
            //Create default entries for any CBs that weren't in the database config file.
            foreach (CelestialBody body in BodyList)
            {
                if (!IgnoreData.ContainsKey(body))
                {
                    if (body.Radius < 100) //Set Kopernicus barycenter and binaries to ignore.
                    {
                        IgnoreData[body] = new BodyIgnoreData(true, true, true, true);
                    }
                    else
                    {
                        IgnoreData[body] = new BodyIgnoreData(false, false, false, false);
                    }
                    
                }
            }

            //Load the NothingHere dictionary from the database config file.
            foreach (ConfigNode.Value value in cfg.GetNode("RESEARCHBODIES").GetNode("NOTHING").values)
            {
                if (value.name == "text")
                    NothingHere.Add(value.value);
            }
            
            LoadModDatabaseNodes();
            
            //So this is deprecated? Checks all CBs are in the Priority dictionary. Any that aren't are added with priority set to 3.
            foreach (CelestialBody cb in BodyList)
            {
                if (!Priority.Keys.Contains(cb) && !IgnoreBodies.Contains(cb))
                {
                    Priority[cb] = 3;
                    RSTLogWriter.Log("Config not found for {0}, priority set to 3." , cb.GetName());
                }
            }

            chances = int.Parse(cfg.GetNode("RESEARCHBODIES").GetValue("chances"));
            RSTLogWriter.Log_Debug("Chances to get a body is set to {0}" , chances);
            enableInSandbox = bool.Parse(cfg.GetNode("RESEARCHBODIES").GetValue("enableInSandbox"));
            allowTSlevel1 = bool.Parse(cfg.GetNode("RESEARCHBODIES").GetValue("allowTrackingStationLvl1"));
            if (cfg.GetNode("RESEARCHBODIES").HasValue("useAppLauncher"))
            {
                UseAppLauncher = bool.Parse(cfg.GetNode("RESEARCHBODIES").GetValue("useAppLauncher"));
            }
            RSTLogWriter.Log_Debug("Loaded gamemode-related information : enable mod in sandbox = {0}, allow tracking with Tracking station lvl 1 = {1}" , enableInSandbox , allowTSlevel1);


            // Load locales for OnDiscovery - Locales are loaded Immediately gamescene. Before this is loaded in MainMenu.
            if (Locales.currentLocale.LocaleId != "en")
            {
                foreach (CelestialBody body in BodyList)
                {
                    if (Locales.currentLocale.Values.ContainsKey("discovery_" + body.GetName()) && DiscoveryMessage.ContainsKey(body.GetName()))
                    {
                        DiscoveryMessage[body.GetName()] = Locales.currentLocale.Values["discovery_" + body.GetName()];
                    }
                }
            }

            foreach (CelestialBody body in BodyList)
            {
                CelestialBodyInfo bodyinfo = new CelestialBodyInfo(body.name);
                if (body.Radius < 100)  //This body is a barycenter
                {
                    bodyinfo.KOPbarycenter = true;
                }
                else
                {
                    if (body.referenceBody.Radius < 100) // This Bodies Reference Body has a Radius < 100m. IE: It's Part of a Barycenter.
                    {
                        bodyinfo.KOPrelbarycenterBody = body.referenceBody; //Yeah so what... well we need it for pass 2 below.
                    }
                }
                CelestialBodies.Add(body, bodyinfo);
            }
            //Now we look back through any CBs that were related to a barycenter body.
            foreach (var CB in CelestialBodies.Where(a => a.Value.KOPrelbarycenterBody != null))
            {
                //So does this body have any orbintingBodies?
                //If it does we need to somehow find and link any related Orbit Body.
                foreach (CelestialBody orbitingBody in CB.Key.orbitingBodies)
                {
                    CelestialBody findOrbitBody =
                        FlightGlobals.Bodies.FirstOrDefault(a => a.name.Contains(CB.Key.name) && a.name.Contains(orbitingBody.name) && a.name.Contains("Orbit"));
                    //so if we found the related Orbit body we store it into the CelestialBodies dictionary.
                    if (findOrbitBody != null)
                    {
                        CelestialBodies[orbitingBody].KOPrelbarycenterBody = findOrbitBody;
                    }
                }
            }
        }

        /* Not sure why this is here, it isn't used.
        public static T CopyComponent<T>(T original, GameObject destination) where T : Component
        {
            System.Type type = original.GetType();
            Component copy = destination.AddComponent(type);
            System.Reflection.FieldInfo[] fields = type.GetFields();
            foreach (System.Reflection.FieldInfo field in fields)
            {
                field.SetValue(copy, field.GetValue(original));
            }
            return copy as T;
        }*/

        private void LoadModDatabaseNodes()
        {
            string[] sep = new string[] { " " };
            //Load all Mod supplied database config files.
            RSTLogWriter.Log_Debug("Loading mods databases");
            ConfigNode[] modNodes = GameDatabase.Instance.GetConfigNodes("RESEARCHBODIES");
            foreach (ConfigNode node in modNodes)
            {
                if (node.GetValue("loadAs") == "mod")
                {
                    if (node.HasValue("name"))
                        RSTLogWriter.Log_Debug("Loading {0} configuration", node.GetValue("name"));
                    if (node.HasNode("PRIORITIES"))
                    {
                        foreach (CelestialBody body in BodyList)
                        {
                            string name = body.GetName();
                            foreach (ConfigNode.Value value in node.GetNode("PRIORITIES").values)
                            {
                                if (name == value.name)
                                {
                                    Priority[body] = int.Parse(value.value);
                                    RSTLogWriter.Log_Debug("Priority for body {0} set to {1}", name, value.value);
                                }
                                else if ("*" + name == value.name)
                                {
                                    Priority[body] = int.Parse(value.value);
                                    RSTLogWriter.Log_Debug("Priority for body {0} set to {1}, overriding old value.", name, value.value);
                                }
                            }
                        }
                    }
                    if (node.HasNode("ONDISCOVERY"))
                    {
                        foreach (CelestialBody body in BodyList)
                        {
                            foreach (ConfigNode.Value value in node.GetNode("ONDISCOVERY").values)
                            {
                                if (value.name == body.GetName() || value.name == "*" + body.GetName())
                                    DiscoveryMessage[value.name] = value.value;
                            }
                            if (!DiscoveryMessage.ContainsKey(body.GetName()))
                                DiscoveryMessage[body.GetName()] = "Now tracking " + body.GetName() + " !";
                        }
                    }
                    //if (node.HasValue("blurredTextures"))
                    //{
                    //    foreach (CelestialBody body in BodyList)
                    //    {
                    //        foreach (string str in Directory.GetFiles(node.GetValue("blurredTextures")))
                    //        {
                    //            if (str.Contains(body.GetName()))
                    //                BlurredTextures[body] = GameDatabase.Instance.GetTexture(node.GetValue("blurredTextures") + "/" + body.GetName(), false);
                    //        }
                    //    }
                    //}
                    if (node.HasNode("IGNORE"))
                    {
                        foreach (ConfigNode.Value value in node.GetNode("IGNORE").values)
                        {
                            if (value.name == "body")
                            {
                                foreach (CelestialBody cb in BodyList)
                                {
                                    if (value.value == cb.GetName())
                                    {
                                        IgnoreData[cb] = new BodyIgnoreData(false, false, false, false);
                                        RSTLogWriter.Log_Debug("Added {0}  to the ignore list (pre-1.5 method !)", cb.GetName());
                                    }
                                }
                            }
                            else if (value.name == "!body")
                            {
                                foreach (CelestialBody cb in BodyList)
                                {
                                    if (value.value == cb.GetName() && IgnoreBodies.Contains(cb))
                                    {
                                        IgnoreData[cb] = new BodyIgnoreData(true, true, true, true);
                                        RSTLogWriter.Log_Debug("Removed {0}  from the ignore list (pre-1.5 method!)", cb.GetName());
                                    }
                                }
                            }
                        }
                    }
                    if (node.HasNode("IGNORELEVELS"))
                    {
                        foreach (ConfigNode.Value value in node.GetNode("IGNORELEVELS").values)
                        {
                            foreach (CelestialBody body in BodyList)
                            {
                                if (body.GetName() == value.name)
                                {
                                    string[] args;
                                    args = value.value.Split(sep, StringSplitOptions.RemoveEmptyEntries);
                                    IgnoreData[body] = new BodyIgnoreData(bool.Parse(args[0]), bool.Parse(args[1]), bool.Parse(args[2]), bool.Parse(args[3]));

                                    RSTLogWriter.Log_Debug("Body Ignore Data for {0} : {1}", body.GetName(), IgnoreData[body].ToString());
                                }
                            }
                        }
                    }
                }
            }
        }
    }

    public class BodyIgnoreData
    {
        public bool Easy, Normal, Medium, Hard;
        public BodyIgnoreData(bool easy, bool normal, bool medium, bool hard)
        {
            Easy = easy;
            Normal = normal;
            Medium = medium;
            Hard = hard;
        }

        public bool GetLevel(Level lvl)
        {
            bool x;
            switch (lvl)
            {
                case Level.Easy:
                    x = this.Easy;
                    break;
                case Level.Normal:
                    x = this.Normal;
                    break;
                case Level.Medium:
                    x = this.Medium;
                    break;
                default:
                    x = this.Hard;
                    break;
            }
            return x;
        }
        public override string ToString()
        {
            return this.Easy + " " + this.Normal + " " + this.Medium + " " + this.Hard;
        }
    }

    

    
}