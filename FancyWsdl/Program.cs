using System;
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

				// name in XmlElementAttribute
				foreach (Match match in Regex.Matches(content,@"\[(?<xmlElementAttribute>System.Xml.Serialization.XmlElementAttribute\()(""(?<elementName>[^""]+)"", )?Form=System.Xml.Schema.XmlSchemaForm.Unqualified\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+)"))
				{
					string xmlElementAttribute = match.Groups["xmlElementAttribute"].Value;
					string elementName = match.Groups["elementName"].Value;
					string propertyType = match.Groups["propertyType"].Value;
					string propertyName = match.Groups["propertyName"].Value;

					if (String.IsNullOrEmpty(elementName))
						content = content.Replace(match.Value,match.Value.Replace(xmlElementAttribute,xmlElementAttribute+"\""+propertyName+"\", "));
				}

				// autoimplemented getters & setters
				foreach (Match match in Regex.Matches(content,@"public (?<propertyType>\S+) (?<propertyName>\S+) (?<getterSetter>\{\s+get \{\s+return [^;]+;\s+}\s+set \{\s+[^;]+;\s+}\s+})"))
				{
					string propertyType = match.Groups["propertyType"].Value;
					string propertyName = match.Groups["propertyName"].Value;
					string getterSetter = match.Groups["getterSetter"].Value;

					content = content.Replace(match.Value,match.Value.Replace(getterSetter,"{ get; set; }"));
					content = Regex.Replace(content,$"private {Regex.Escape(propertyType)} {Regex.Escape(propertyName)}Field;\\s*","");
					// content = Regex.Replace(content,$"\\b{Regex.Escape(propertyName)}Field\\b",propertyName);
				}

				File.WriteAllText(path,content,encoding);
			}
		}
	}
}
