﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Threading;
using System.Windows.Forms;
using BpOmniaBridge.CommandList;

namespace BpOmniaBridge.CommandUtility
{
    public class WriteCommand
    {
        private static string cmnDocPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        private static string folderPath = Path.Combine(cmnDocPath, "BpOmniaBridge", "temp_files");
        private XDocument command;
        private string fileName;

        public WriteCommand(string name)
        {
            command = new XDocument(
                new XElement("OmniaXB")
                );
            fileName = name;
        }

        public void BasicCommand(string system, string cmd)
        {
            command.Element("OmniaXB").Add(new XElement(system));
            command.Element("OmniaXB").Element(system).Add(new XElement(cmd));
        }

        public void AddCommands(string system, string cmd, string[] paramsNames, string[] paramsValues, bool guidParam = false, int guidIndex = 0)
        {
            BasicCommand(system, cmd);

            int index = 0;
            foreach (string prm in paramsNames)
            {
                if (guidParam && index == guidIndex )
                {
                    Guid prmValue;
                    Guid.TryParse(paramsValues[index], out prmValue);
                    command.Element("OmniaXB").Element(system).Element(cmd).Add(new XElement(prm, prmValue));
                }

                command.Element("OmniaXB").Element(system).Element(cmd).Add(new XElement(prm, paramsValues[index]));
                index += 1;
            }
        }

        public void Save()
        {
            //TODO: make this smarter!!! XDocument creates the first line with xml details which are not required 
            var filePath = Path.Combine(folderPath, fileName);
            command.Save(filePath);
            string[] lines = File.ReadAllLines(filePath);
            lines = lines.Skip(1).ToArray();
            File.WriteAllLines(filePath, lines);
        }

    }

    public class Command
    {   
        public Command(string name, string sys, string cmmd, bool guid = false, int guidIndex = 0)
        {
            FileName = name;
            System = sys;
            Cmd = cmmd;
            prmGuid = guid;
            indexGuid = guidIndex;
            ParNames = new string[] { };
            ParValues = new string[] { };
        }

        public void AddParams(string[] names, string[] values)
        {
            ParNames = names;
            ParValues = values;
        }

        //Properties
        private string FileName;
        private string System;
        private string Cmd;
        private string[] ParNames;
        private string[] ParValues;
        private bool prmGuid;
        private int indexGuid;

        public void Send()
        {
            var command = new WriteCommand(FileName + ".in");
            command.AddCommands(System, Cmd, ParNames, ParValues, prmGuid, indexGuid);
            command.Save();
            Utility.Utility.Log("CMD => " + FileName);
        }
    }

    public class ReadCommands
    {
        private static string cmnDocPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
        private static string folderPath = Path.Combine(cmnDocPath, "BpOmniaBridge", "temp_files");

        //Initializer name of the file and type of result
        public ReadCommands(string name, string system, string cmd)
        {
            FileName = name;
            CmdSystem = system;
            Cmd = cmd;
            CheckFileExists();
        }

        //Properties
        private string FileName;
        private string CmdSystem;
        private string Cmd;
        public bool FileExist;
        public List<string> ResultValues;
        public List<string> ResultKeys;

        #region Methods
        public void Read()
        {
            ResultValues = new List<string> { };
            ResultKeys = new List<string> { };
            if (FileExist)
            {
                var filePath = Path.Combine(folderPath, FileName + ".out");
                //get all the elemnts under the command key
                IEnumerable<XElement> elements = XDocument.Load(filePath).Elements("OmniaXB").Elements(CmdSystem).Elements(Cmd).Elements();
                foreach( XElement element in elements)
                {
                    ResultKeys.Add(element.Name.ToString());
                    ResultValues.Add(element.Value);
                }
                DeleteFile();
                Utility.Utility.Log("Read => done");
            }
            else
            {
                ResultKeys.Add("Result");
                ResultValues.Add("NAK");
            }
                
        }
                
        private void CheckFileExists()
        {
            Utility.Utility.Log("Read => start");
            // Set timeout to 30 seconds
            // ASK: is this ok?
            var timeout = DateTime.Now.Add(TimeSpan.FromSeconds(30));
            var fullPath = Path.Combine(folderPath, FileName + ".out");
            while (!File.Exists(fullPath))
            {
                if (DateTime.Now > timeout)
                {
                    Utility.Utility.Log("timeout => " + FileName + ".out has not been created within 20 secs");
                    FileExist = false;
                    MessageBox.Show("Omnia is not answering. Check Bridge log file", "Communication Issue", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    DeleteFile(".in");
                    break;
                }
                Thread.Sleep(200);
            }
            FileExist = File.Exists(fullPath);
            Read();
        }

        private void DeleteFile(string in_out = ".out")
        {
            var filePath = Path.Combine(folderPath, FileName + in_out);
            File.Delete(filePath);
        }
    }
    #endregion

    #region Archive Methods

    public class Archive
    {
        //Initializer: it expect 2 arrays with ID, FirstName, Middlename, LastName, DOB, Gender, Ethnicity, Height and Weight
        public Archive(string[] keys, string[] values)
        {
            //removing the height and weight
            var keysList = keys.ToList();
            var valuesList = values.ToList();
            keysList.RemoveRange(7, 2);
            valuesList.RemoveRange(7, 2);
            keysArray = keysList.ToArray();
            valuesArray = valuesList.ToArray();

            //build array to create visit card (ID, Height, Weight)
            var visitKeysList = keys.ToList();
            var visitValuesList = values.ToList();
            visitKeysList.RemoveRange(0, 7);
            visitValuesList.RemoveRange(0, 7);
            visitKeysArray = visitKeysList.ToArray();
            visitValuesArray = visitValuesList.ToArray();
        }

        public void SetRecordID(string id)
        {
            recordID = id;

            // add the recordID to the array to create a new visit card
            var visitKeysList = visitKeysArray.ToList();
            var visitValuesList = visitValuesArray.ToList();
            visitKeysList.Insert(0, "SubjectID");
            visitValuesList.Insert(0, recordID);
            visitKeysArray = visitKeysList.ToArray();
            visitValuesArray = visitValuesList.ToArray();
        }

        //Properties
        private string[] keysArray;
        private string[] valuesArray;
        private string[] visitKeysArray;
        private string[] visitValuesArray;
        private string recordID;

        public string CreateSubject()
        {
            return new CommandList.CommandList().SelectCreateSubject(keysArray, valuesArray);
        }

        public string TodayVisitCard()
        {
            string[] keys = { "RecordID" };
            string[] ids = { recordID };
            List<string> visitList = new CommandList.CommandList().GetSubjectVisitList(keys, ids);
            int index = visitList.Count / 2;
            int startIndex = index;
            string result = "NAK";
            if (visitList.ElementAt(index) != "NAK")
            {
                if (Int32.Parse(visitList.ElementAt(index)) > 0)
                {
                    bool found = false;
                    //existing visit card - check if the one of today exist already
                    //visitList.RemoveAt(0);
                    for (int i = index + 1; i < visitList.Count; i++)
                    {
                        //if (visitList.ElementAt(i) == DateTime.Today.ToString("yyyyMMdd"))
                        if (visitList.ElementAt(i) == "20130328")
                        {
                            found = true;
                            index = i;
                            break;
                        }
                    }

                    if (found)
                    {
                        //return ID
                        string id = visitList.ElementAt((index - startIndex)).Remove(0, 3);
                        result = id;
                    }
                    else
                    {
                        //create new visit card
                        string id = new CommandList.CommandList().CreateVisit(visitKeysArray, visitValuesArray);
                        result = id;
                    }

                }
            }

            return result;
        }

        public bool ExportTests(string id)
        {
            var cmnDocPath = System.Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            var filePath = Path.Combine(cmnDocPath, "BpOmniaBridge", "temp_files", "tests.xml");

            string[] keys = new string[] { "RecordID", "Filename", "Type" };
            string[] values = new string[] { id, filePath, "V" };

            return new CommandList.CommandList().ExportData(keys, values);
        }

        // first element is the bool to see if everything went well
        // the rest would be: Diasgnosis/PEF/FEV1/FVC/PEFPOST/FEV1POST/FVCPOST/O2sat
        public List<string> ReadExportDataFile()
        {
            List<string> results = new List<string> { };
            var cmnDocPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDocuments);
            var filePath = Path.Combine(cmnDocPath, "BpOmniaBridge", "temp_files", "tests.xml");
            //get all the elemnts under the command key
            IEnumerable<XElement> elements = XDocument.Load(filePath).Elements("COSMED_OMNIA_EXPORT").Elements("Subject").Elements("Visit").Elements();

            var type = FindTypeOfTest(filePath);

            FindTestToPrint();
            FindDataToImport();

            return results;
        }

        //return 
        // BC = bronco costrictor challege when FVCPOSTBD and FCVPOST is found
        // BD = broco dilator when FVCPOSTBD only is found
        // PRE = only FVC is found
        // ASK: double check this
        private string FindTypeOfTest(string filePath)
        {
            string type = "";
            bool foundFVC = false;
            bool foundFVCPOSTBD = false;
            bool foundFVCPOST = false;
            Guid FVC = new Guid("11A4801F-7977-4D3E-8D1E-6CA0BE52E604");
            Guid FVCPOSTBD = new Guid("EEF9F5EE-605F-4E4A-AFE3-43E6E57C6C4A");
            Guid FVCPOST = new Guid ("80266DA7-A2DA-4013-B9F6-599FA6169392");

            IEnumerable<XElement> elements = XDocument.Load(filePath).Elements("COSMED_OMNIA_EXPORT").Elements("Subject").Elements("Visit").Elements("Test");

            foreach (XElement element in elements)
            {
                IEnumerable<XElement> testInfos = element.Elements();
                foreach (XElement info in testInfos)
                {                 
                    if (info.Name.ToString()=="TestType")
                    {
                        Guid currentTest = new Guid(info.Value);
                        if (!foundFVC) { foundFVC = (currentTest == FVC); }
                        if (!foundFVCPOST) { foundFVCPOST = (currentTest == FVCPOST); }
                        if (!foundFVCPOSTBD) { foundFVCPOSTBD = (currentTest == FVCPOSTBD); }
                        goto nextElement;
                    }
                }
                nextElement: ;

            }

            if (foundFVCPOST && foundFVCPOSTBD)
            {
                type = "BC";
            }
            else
            {
                if (foundFVCPOSTBD && foundFVC)
                {
                    type = "BD";
                }
                else { type = "PRE";}
            }

            return type;
        }
    }
    #endregion
}
