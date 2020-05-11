using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;

namespace FancyWsdl
{
	class Program
	{
		static void Main(string[] args)
		{
			static string firstLetterUppercase(string s) => Char.ToUpper(s[0])+s.Substring(1);

			foreach (string path in args)
			{
				Encoding encoding = Encoding.UTF8;
				string fileContent = File.ReadAllText(path,encoding);

				// enumerate all class definitions
				foreach (Match classMatch in Regex.Matches(fileContent,@"^(?<space>\s*)public partial class (?<className>\S+).*?\n(\k<space>)}",RegexOptions.Singleline|RegexOptions.Multiline))
				{
					string className = classMatch.Groups["className"].Value;
					string classContent = classMatch.Value;

					// property name in XmlElementAttribute
					foreach (Match match in Regex.Matches(classContent,@"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlElement(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\]\s+public (?<propertyType>\S+) (?<propertyName>\S+) "))
					{
						string xmlElementAttribute = match.Groups["xmlElementAttribute"].Value;
						string elementName = match.Groups["elementName"].Value;
						string propertyType = match.Groups["propertyType"].Value;
						string propertyName = match.Groups["propertyName"].Value;

						if (String.IsNullOrEmpty(elementName))
							classContent = classContent.Replace(match.Value,match.Value.Replace(xmlElementAttribute,xmlElementAttribute+"\""+propertyName+"\", "));
					}

					// autoimplemented getters & setters
					foreach (Match match in Regex.Matches(classContent,@"public (?<propertyType>\S+) (?<propertyName>\S+) (?<getterSetter>\{\s+get \{\s+return this\.(?<fieldName>[^;]+);\s+}\s+set \{\s+[^;]+;\s+\}\s+\})"))
					{
						string propertyType = match.Groups["propertyType"].Value;
						string propertyName = match.Groups["propertyName"].Value;
						string getterSetter = match.Groups["getterSetter"].Value;
						string fieldName = match.Groups["fieldName"].Value;

						classContent = classContent.Replace(match.Value,match.Value.Replace(getterSetter,"{ get; set; }"));
						classContent = Regex.Replace(classContent,$@"private {Regex.Escape(propertyType)} {Regex.Escape(fieldName)};\s*","");
						classContent = Regex.Replace(classContent,$@"\b{Regex.Escape(fieldName)}\b",propertyName);
					}

					// property names with uppercase first letter
					foreach (Match match in Regex.Matches(classContent,@"(?<pre>public \S+ )(?<propertyName>\S+)(?<post> \{)"))
					{
						string pre = match.Groups["pre"].Value;
						string propertyName = match.Groups["propertyName"].Value;
						string post = match.Groups["post"].Value;

						string newPropertyName = firstLetterUppercase(propertyName);

						classContent = classContent.Replace(match.Value,pre+newPropertyName+post);
						classContent = classContent.Replace($"this.{propertyName} ",$"this.{newPropertyName} ");
						classContent = classContent.Replace($@".SoapHeaderAttribute(""{propertyName}"")",$@".SoapHeaderAttribute(""{newPropertyName}"")");
					}

					// compute *Specified properties
					foreach (Match match in Regex.Matches(classContent,@"\[(System.Xml.Serialization.)?XmlIgnore(Attribute)?\(\)\]\s+public bool (?<propertyName>\S+)Specified (?<getterSetter>\{ get; set; \}|\{\s+get \s+[^;]+;\s+\}\s+\s+set \{\s+[^;]+;\s+\}\s+\})"))
					{
						string propertyName = match.Groups["propertyName"].Value;
						string getterSetter = match.Groups["getterSetter"].Value;

						classContent = Regex.Replace(classContent,$@"(?<pre>public \S+)(?<post> {propertyName} )",m => m.Groups["pre"].Value+"?"+m.Groups["post"].Value);
						classContent = classContent.Replace(match.Value,match.Value.Replace(getterSetter,$"=> this.{propertyName}.HasValue;"));
					}

					fileContent = fileContent.Replace(classMatch.Value,classContent);
				}


				foreach (Match classMatch in Regex.Matches(fileContent,@"(\[(?<xmlRootAttribute>(System.Xml.Serialization.)?XmlRoot(Attribute)?\()(""(?<rootName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)(?<classDefinition>public partial class (?<className>\S+) )"))
				{
					string xmlRootAttribute = classMatch.Groups["xmlRootAttribute"].Value;
					string rootName = classMatch.Groups["rootName"].Value;
					string space = classMatch.Groups["space"].Value;
					string classDefinition = classMatch.Groups["classDefinition"].Value;
					string className = classMatch.Groups["className"].Value;

					// class name in XmlRootAttribute
					if (String.IsNullOrEmpty(xmlRootAttribute))
						fileContent = fileContent.Replace(classMatch.Value,classMatch.Value.Replace(classDefinition,$"[System.Xml.Serialization.XmlRootAttribute(\"{className}\")]"+space+classDefinition));
					else if (String.IsNullOrEmpty(rootName))
						fileContent = fileContent.Replace(classMatch.Value,classMatch.Value.Replace(xmlRootAttribute,xmlRootAttribute+"\""+className+"\", "));

					// class name with uppercase first letter
					fileContent = Regex.Replace(fileContent,$@"(?<!"")\b{Regex.Escape(className)}\b(?!"")",firstLetterUppercase(className));
				}


				// use usings
				string[] usings = new[] { "System","System.CodeDom.Compiler","System.ComponentModel","System.Diagnostics","System.Threading","System.Web.Services","System.Web.Services.Description","System.Web.Services.Protocols","System.Xml.Schema","System.Xml.Serialization" };
				foreach (string usingNamespace in usings.OrderByDescending(u => u.Length))
					fileContent = fileContent.Replace(usingNamespace+".","");
				fileContent = Regex.Replace(fileContent,@"using \S+;\s*","");
				foreach (string usingNamespace in usings.OrderBy(u => u).Reverse())
					fileContent = $"using {usingNamespace};"+Environment.NewLine+fileContent;

				// use attribute shortcut
				fileContent = fileContent.Replace("Attribute(","(");


				File.WriteAllText(path,fileContent,encoding);
			}
		}
	}
}

