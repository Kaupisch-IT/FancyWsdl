using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace FancyWsdl
{
	class Program
	{
		static void Main(string[] args)
		{
			foreach (string path in args)
			{
				Encoding encoding = Encoding.UTF8;
				string content = File.ReadAllText(path,encoding);


				foreach (Match match in Regex.Matches(content,@"\[(?<xmlElementAttribute>System.Xml.Serialization.XmlElementAttribute\()(""(?<elementName>[^""]+)"", )?Form=System.Xml.Schema.XmlSchemaForm.Unqualified\)\]\s+public (?<propertyType>\S+)(\[\])? (?<propertyName>\S+) (?<getterSetter>\{\s+get \{\s+return [^;]+;\s+}\s+set \{\s+[^;]+;\s+}\s+})"))
				{
					string xmlElementAttribute = match.Groups["xmlElementAttribute"].Value;
					string elementName = match.Groups["elementName"].Value;
					string propertyType = match.Groups["propertyType"].Value;
					string propertyName = match.Groups["propertyName"].Value;
					string getterSetter = match.Groups["getterSetter"].Value;

					if (String.IsNullOrEmpty(elementName))
						content = content.Replace(match.Value,match.Value.Replace(xmlElementAttribute,xmlElementAttribute+"\""+propertyName+"\", "));

					Debug.WriteLine(propertyName);
				}

				File.WriteAllText(path,content,encoding);
			}
		}
	}
}
