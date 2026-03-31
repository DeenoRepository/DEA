using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace EquipmentFailureAnalysis.Utility
{
    public class XmlDataDecoder
    {
        private XmlDocument xmlDocument;
        private ObservableCollection<EquipmentInfo> ?equipmentCollection;

        public XmlDataDecoder() 
        { 
            xmlDocument = new System.Xml.XmlDocument();
            xmlDocument.Load("Data/EquipmentData.xml");
        }

        public ObservableCollection<EquipmentInfo> DecodeEquipment()
        {
            XmlNodeList xmlNodeList = xmlDocument.GetElementsByTagName("item");

            string pattern = @"Оборудование.*?\)\s*(?<Name>.*?)\t+(?<ID>\d+)";

            equipmentCollection = new ObservableCollection<EquipmentInfo>();

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                Match match = Regex.Match(xmlNode.InnerText, pattern);

                if (match.Success)
                {
                    if (xmlNode["status"]?.InnerText == "Решен")
                    {
                        equipmentCollection.Add(new EquipmentInfo
                        {
                            Title = match.Groups["Name"].Value.Trim(),
                            Uid = int.Parse(match.Groups["ID"].Value),
                            Issue = new Issue
                            {
                                Created = DateTime.Parse(xmlNode["created"].InnerText),
                                Resolved = DateTime.Parse(xmlNode["resolved"].InnerText),
                                Description = xmlNode["description"].InnerText
                            }
                        });
                    }
                }
            }
            return equipmentCollection;
        }
    }
}
