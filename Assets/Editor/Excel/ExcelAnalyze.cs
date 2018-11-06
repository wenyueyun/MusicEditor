using Excel;
using LitJson;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Data;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ExcelAnalyze
{

    private static List<string> desc_list = new List<string>();
    private static List<string> key_list = new List<string>();
    private static List<string> type_list = new List<string>();
    private static Dictionary<string, List<ConfigData>> data_dic = new Dictionary<string, List<ConfigData>>();

    [MenuItem("Window/ExcelAnalyze")]
    public static void GenerateXml()
    {
        string path = EditorUtility.OpenFilePanel("Select xlsx file", Path.Combine(Application.dataPath, "Execl"), "xlsx");
        if (string.IsNullOrEmpty(path) == false)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read))
            {
                IExcelDataReader excelReader = ExcelReaderFactory.CreateOpenXmlReader(stream);
                DataSet result = excelReader.AsDataSet();
                DataTableCollection tables = result.Tables;
                foreach (DataTable item in tables)
                {
                    AnalysisExcelData(item);
                }
            }
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// 解析配置表数据
    /// </summary>
    /// <param name="table">单个表数据</param>
    private static void AnalysisExcelData(DataTable table)
    {
        desc_list.Clear();
        key_list.Clear();
        type_list.Clear();
        data_dic.Clear();
        for (int i = 0; i < table.Rows.Count; i++)
        {
            object[] objs = table.Rows[i].ItemArray;
            List<ConfigData> row_list = new List<ConfigData>();

            for (int j = 0; j < objs.Length; j++)
            {
                if (i == 0)
                {
                    desc_list.Add(objs[j].ToString());
                }
                else if (i == 1)
                {
                    key_list.Add(objs[j].ToString());
                }
                else if (i == 2)
                {
                    type_list.Add(objs[j].ToString());
                }
                else
                {
                    ConfigData data = new ConfigData();
                    data.type = type_list[j];
                    data.key = key_list[j];
                    data.value = objs[j].ToString();
                    row_list.Add(data);
                }
            }
            if (row_list.Count > 0 && !string.IsNullOrEmpty(row_list[0].value))
            {
                data_dic.Add(row_list[0].value, row_list);
            }
        }

        GenerateParseJson(table.TableName);
    }

    private static void GenerateParseJson(string tableName)
    {
        JsonData json = new JsonData();

        JsonData config = new JsonData();
        foreach (var item in data_dic.Values)
        {
            JsonData data = new JsonData();
            for (int j = 0; j < item.Count; j++)
            {
                ConfigData temp = item[j];
                data[temp.key] = temp.value;
            }
            config.Add(data);
        }

        json[tableName] = config;

        File.WriteAllText(Path.Combine(Application.dataPath + "/Export/Excel", tableName + ".json"), JsonFormat(json.ToJson()));

        Debug.Log(string.Format("{0}  ------>  解析成功", tableName));
    }

    //将json数据进行格式化
    public static string JsonFormat(string str)
    {
        JsonSerializer serializer = new JsonSerializer();
        StringReader sReader = new StringReader(str);
        JsonTextReader jReader = new JsonTextReader(sReader);
        object readerObj = serializer.Deserialize(jReader);
        if (readerObj != null)
        {
            StringWriter sWriter = new StringWriter();
            JsonTextWriter jWriter = new JsonTextWriter(sWriter)
            {
                Formatting = Newtonsoft.Json.Formatting.Indented,
                Indentation = 2,
                IndentChar = ' '
            };
            serializer.Serialize(jWriter, readerObj);
            return sWriter.ToString();
        }
        else
        {
            return str;
        }
    }
}



public class ConfigData
{
    public string type;
    public string key;
    public string value;
}



