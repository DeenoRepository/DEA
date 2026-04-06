using EquipmentFailureAnalysis.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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

            equipmentCollection = new ObservableCollection<EquipmentInfo>();

            foreach (XmlNode xmlNode in xmlNodeList)
            {
                // only processed (resolved) items
                if (xmlNode["status"]?.InnerText != "Решен")
                    continue;

                // try to get equipment custom field (contains name and inventory id)
                var equipmentNode = xmlNode.SelectSingleNode("customfields/customfield[@id='customfield_10519']/customfieldvalues/customfieldvalue");
                string? equipmentText = equipmentNode?.InnerText?.Trim();

                // fallback: try to extract from title/summary if custom field not present
                string title = string.Empty;
                int uid = 0;
                string? inventoryNumber = null;

                if (!string.IsNullOrEmpty(equipmentText))
                {
                    // equipmentText expected like: "<Name>\t<ID>" or ending with digits
                    var m = Regex.Match(equipmentText, @"^(?<Name>.*?)[\t\s]+(?<ID>\d+)$");
                    if (m.Success)
                    {
                        title = m.Groups["Name"].Value.Trim();
                        uid = int.Parse(m.Groups["ID"].Value);
                        inventoryNumber = m.Groups["ID"].Value;
                    }
                    else
                    {
                        title = equipmentText;
                    }
                }
                else
                {
                    // try to parse from summary/title
                    var summary = xmlNode["summary"]?.InnerText ?? xmlNode["title"]?.InnerText ?? string.Empty;
                    var m = Regex.Match(summary, @"^(.*?)[\t\s]+(?<ID>\d+)$");
                    if (m.Success)
                    {
                        title = m.Groups[1].Value.Trim();
                        uid = int.Parse(m.Groups["ID"].Value);
                        inventoryNumber = m.Groups["ID"].Value;
                    }
                    else
                    {
                        title = summary;
                    }
                }

                DateTime.TryParse(xmlNode["created"]?.InnerText, out DateTime start);
                DateTime.TryParse(xmlNode["resolved"]?.InnerText, out DateTime end);

                string description = xmlNode["description"]?.InnerText ?? string.Empty;
                if (!string.IsNullOrEmpty(description))
                {
                    // remove paragraph tags <p> and </p> (case-insensitive)
                    description = Regex.Replace(description, "</?p\\s*>", string.Empty, RegexOptions.IgnoreCase);
                    description = description.Trim();
                    // capitalize first letter
                    if (description.Length > 0)
                        description = char.ToUpper(description[0], System.Globalization.CultureInfo.CurrentCulture) + (description.Length > 1 ? description.Substring(1) : string.Empty);
                }

                // issue type from custom field (Тип проводимых работ)
                var typeNode = xmlNode.SelectSingleNode("customfields/customfield[@id='customfield_10501']/customfieldvalues/customfieldvalue");
                string? typeText = typeNode?.InnerText?.Trim();

                EquipmentFailureAnalysis.Models.IssueType issueType = EquipmentFailureAnalysis.Models.IssueType.Ремонт;
                if (!string.IsNullOrEmpty(typeText) && typeText.IndexOf("Настрой", StringComparison.OrdinalIgnoreCase) >= 0)
                    issueType = EquipmentFailureAnalysis.Models.IssueType.Настройка;
                else if (!string.IsNullOrEmpty(typeText) && typeText.IndexOf("Ремонт", StringComparison.OrdinalIgnoreCase) >= 0)
                    issueType = EquipmentFailureAnalysis.Models.IssueType.Ремонт;

                // responsible: prefer assignee, fallback to custom field 10502
                string? responsible = xmlNode["assignee"]?.InnerText;
                if (string.IsNullOrEmpty(responsible))
                {
                    var respNode = xmlNode.SelectSingleNode("customfields/customfield[@id='customfield_10502']/customfieldvalues/customfieldvalue");
                    responsible = respNode?.InnerText?.Trim();
                }

                var issue = new Issue
                {
                    Start = start,
                    End = end,
                    Description = description,
                    Type = issueType,
                    Responsible = responsible
                };

                // find existing equipment entry by uid (if uid==0 use title)
                EquipmentInfo? equipment = null;
                if (uid != 0)
                    equipment = equipmentCollection.FirstOrDefault(e => e.Uid == uid);
                if (equipment == null)
                    equipment = equipmentCollection.FirstOrDefault(e => e.Title == title);

                if (equipment == null)
                {
                    equipment = new EquipmentInfo
                    {
                        Title = string.IsNullOrEmpty(title) ? "Unknown" : title,
                        Uid = uid,
                        InventoryNumber = inventoryNumber
                    };
                    equipmentCollection.Add(equipment);
                }

                equipment.Issues.Add(issue);
            }

            return equipmentCollection;
        }
    }
}
