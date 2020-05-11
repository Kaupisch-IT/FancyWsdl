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
				string fileContent = File.ReadAllText(path,encoding);

				// enumerate all class definitions
				foreach (Match classMatch in Regex.Matches(fileContent,@"^(?<space>\s*)public partial class (?<className>\S+).*?\n(\k<space>)}",RegexOptions.Singleline|RegexOptions.Multiline))
				{
					string className = classMatch.Groups["className"].Value;
					string classContent = classMatch.Value;

					// name in XmlElementAttribute
					foreach (Match match in Regex.Matches(fileContent,@"\[(?<xmlElementAttribute>System.Xml.Serialization.XmlElementAttribute\()(""(?<elementName>[^""]+)"", )?Form=System.Xml.Schema.XmlSchemaForm.Unqualified\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+) "))
					{
						string xmlElementAttribute = match.Groups["xmlElementAttribute"].Value;
						string elementName = match.Groups["elementName"].Value;
						string propertyType = match.Groups["propertyType"].Value;
						string propertyName = match.Groups["propertyName"].Value;

						if (String.IsNullOrEmpty(elementName))
							classContent = classContent.Replace(match.Value,match.Value.Replace(xmlElementAttribute,xmlElementAttribute+"\""+propertyName+"\", "));
					}

					// autoimplemented getters & setters
					foreach (Match match in Regex.Matches(fileContent,@"public (?<propertyType>\S+) (?<propertyName>\S+) (?<getterSetter>\{\s+get \{\s+return this\.(?<fieldName>[^;]+);\s+}\s+set \{\s+[^;]+;\s+}\s+})"))
					{
						string propertyType = match.Groups["propertyType"].Value;
						string propertyName = match.Groups["propertyName"].Value;
						string getterSetter = match.Groups["getterSetter"].Value;
						string fieldName = match.Groups["fieldName"].Value;

						classContent = classContent.Replace(match.Value,match.Value.Replace(getterSetter,"{ get; set; }"));
						classContent = Regex.Replace(classContent,$"private {Regex.Escape(propertyType)} {Regex.Escape(fieldName)};\\s*","");
						classContent = Regex.Replace(classContent,$"\\b{Regex.Escape(fieldName)}\\b",propertyName);
					}

					fileContent = fileContent.Replace(classMatch.Value,classContent);
				}

				File.WriteAllText(path,fileContent,encoding);
			}
		}
	}
}
