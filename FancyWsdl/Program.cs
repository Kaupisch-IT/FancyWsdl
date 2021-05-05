using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;

namespace FancyWsdl
{
	class Program
	{
		static void Main(string[] args)
		{
			static string firstLetterUppercase(string s) => Char.ToUpper(s[0])+s.Substring(1);

			if (args.Any())
			{
				string path = args[0];

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

					// auto-implemented getters & setters
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

					// method name in SoapDocumentMethodAttribute
					foreach (Match match in Regex.Matches(classContent,@"(?<soapDocumentMethodAttribute>\[(System.Web.Services.Protocols.)?SoapDocumentMethod(Attribute)?\(""[^""]*""[^\]]*)\)\]\s*\[return: [^]]+\]\s+public \S+ (?<methodName>[^\s\(]+)\("))
					{
						string soapDocumentMethodAttribute = match.Groups["soapDocumentMethodAttribute"].Value;
						string methodName = match.Groups["methodName"].Value;

						string argumentsToAdd = null;
						if (!soapDocumentMethodAttribute.Contains("RequestElementName = "))
							argumentsToAdd += $", RequestElementName = \"{methodName}\"";
						if (!soapDocumentMethodAttribute.Contains("ResponseElementName = "))
							argumentsToAdd += $", ResponseElementName = \"{methodName}Response\"";
						classContent = classContent.Replace(match.Value,match.Value.Replace(soapDocumentMethodAttribute,soapDocumentMethodAttribute+argumentsToAdd));
					}

					// method name with uppercase first letter
					foreach (Match match in Regex.Matches(classContent,@"(?<pre>public \S+ (Begin|End)?)(?<methodName>[^\s\(]+)(?<inter>(Async)?[^\n]*(\n[^\n]*){1,4}this\.(Begin|End)?Invoke(Async)?\()(""(?<methodName2>[^""]+)"")?(?<post>)"))
					{
						string pre = match.Groups["pre"].Value;
						string methodName = match.Groups["methodName"].Value;
						string inter = match.Groups["inter"].Value;
						string methodName2 = match.Groups["methodName2"].Value;
						string post = match.Groups["post"].Value;

						classContent = classContent.Replace(match.Value,pre+firstLetterUppercase(methodName)+inter+((!String.IsNullOrEmpty(methodName2)) ? "nameof("+firstLetterUppercase(methodName2)+")" : "")+post);
					}
					foreach (Match match in Regex.Matches(classContent,@"public void (?<methodName>[^\s\(]+)"))
					{
						string methodName = match.Groups["methodName"].Value;
						classContent = Regex.Replace(classContent,$@"(?<pre>( |\.)){methodName}(?<post>[\(\)])",m => m.Groups["pre"].Value+firstLetterUppercase(methodName)+m.Groups["post"].Value);
					}

					fileContent = fileContent.Replace(classMatch.Value,classContent);
				}

				// enumerate all enum definitions
				foreach (Match enumMatch in Regex.Matches(fileContent,@"^(?<space>\s*)public enum (?<enumName>\S+).*?\n(\k<space>)}",RegexOptions.Singleline|RegexOptions.Multiline))
				{
					string enumName = enumMatch.Groups["enumName"].Value;
					string enumContent = enumMatch.Value;

					// enumerate all enum values
					foreach (Match valueMatch in Regex.Matches(enumContent,@"(\[(?<xmlEnumAttribute>(System.Xml.Serialization.)?XmlEnum(Attribute)?\()(""(?<enumValueName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)(?<enumValue>\S+),"))
					{
						string xmlEnumAttribute = valueMatch.Groups["xmlEnumAttribute"].Value;
						string enumValueName = valueMatch.Groups["enumValueName"].Value;
						string space = valueMatch.Groups["space"].Value;
						string enumValue = valueMatch.Groups["enumValue"].Value;

						// enum values in XmlEnumAttribute
						if (String.IsNullOrEmpty(xmlEnumAttribute))
							enumContent = enumContent.Replace(valueMatch.Value,space+$"[System.Xml.Serialization.XmlEnumAttribute(\"{enumValue}\")]"+valueMatch.Value);

						// enum value with uppercase first letter
						enumContent = Regex.Replace(enumContent,@$"\b{enumValue},",$"{firstLetterUppercase(enumValue)},");
					}
					fileContent = fileContent.Replace(enumMatch.Value,enumContent);
				}

				// enumerate all class definitions
				foreach (Match classMatch in Regex.Matches(fileContent,@"(\[(?<xmlRootAttribute>(System.Xml.Serialization.)?XmlRoot(Attribute)?\()(""(?<rootName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)(?<classDefinition>public (partial class|enum) (?<className>\S+) )"))
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
					fileContent = Regex.Replace(fileContent,$@"(?<!"")\b{Regex.Escape(className)}\b(?!""|(\(\[))",firstLetterUppercase(className));
				}

				// add annotations/documentation from XML schema
				foreach (string schemaUrl in args.Skip(1))
				{
					XmlDocument xmlDocument = new XmlDocument();
					using (XmlTextReader xmlTextReader = new XmlTextReader(schemaUrl))
					{
						xmlTextReader.Namespaces = false;
						xmlDocument.Load(xmlTextReader);
					}

					// enumerate all class & enum definitions
					foreach (Match classMatch in Regex.Matches(fileContent,@"(\[(?<xmlRootAttribute>(System.Xml.Serialization.)?XmlRoot(Attribute)?\()(""(?<rootName>[^""]+)"")?[^\)]*\)\])?(?<space>\s+)public (partial class|enum) (?<className>\S+).*?(\k<space>)}",RegexOptions.Singleline|RegexOptions.Multiline))
					{
						string className = classMatch.Groups["className"].Value;
						string classContent = classMatch.Value;
						string rootName = classMatch.Groups["rootName"].Value;

						static string toSummary(string text,string indentSpace)
						{
							text = HttpUtility.HtmlEncode(text.Trim());
							if (text.Contains("\n"))
								return $"/// <summary>{indentSpace}/// {Regex.Replace(text,"\r?\n",indentSpace+"/// ")}{indentSpace}/// </summary>";
							else
								return $"/// <summary> {text} </summary>";
						}

						// element documentation
						foreach (Match elementMatch in Regex.Matches(classContent,@"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlElement(Attribute)?\()(""(?<elementName>[^""]+)"")?[^\)]*\)\](?<space>\s+)public (?<propertyType>\S+) (?<propertyName>\S+) "))
						{
							string propertyName = elementMatch.Groups["propertyName"].Value;
							string elementName = elementMatch.Groups["elementName"].Value;
							string elementDocumentation = xmlDocument.SelectSingleNode($"/*/*[@name='{rootName}']//*[@name='{elementName}']//*[contains(local-name(),'documentation')]")?.InnerText;

							if (elementDocumentation!=null)
							{
								elementDocumentation = toSummary(elementDocumentation,elementMatch.Groups["space"].Value);
								classContent = Regex.Replace(classContent,@$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*public (?<propertyType>\S+) {propertyName}\b)",m => elementDocumentation+m.Groups["remainder"].Value,RegexOptions.Singleline|RegexOptions.Multiline);
							}
						}

						// operation documentation
						foreach (Match elementMatch in Regex.Matches(classContent,@"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?SoapDocumentMethod(Attribute)?\()(""(?<soapName>[^""]+)"")?[^\)]*\)\](\s*\[[^\n]+\])*(?<space>\s+)public (?<returnType>\S+) (?<methodName>[^(\s]+)\("))
						{
							string methodName = elementMatch.Groups["methodName"].Value;
							string soapName = elementMatch.Groups["soapName"].Value;
							string typeName = xmlDocument.SelectSingleNode($"//*[contains(local-name(),'binding')]/*[contains(local-name(),'operation')]/*[@soapAction='{soapName}']/../../@type")?.InnerText;
							string operationName = xmlDocument.SelectSingleNode($"//*[contains(local-name(),'binding')]/*[contains(local-name(),'operation')]/*[@soapAction='{soapName}']/../@name")?.InnerText;
							if (typeName!=null)
							{
								typeName = Regex.Replace(typeName,@"^[^:]+:","");
								string elementDocumentation = xmlDocument.SelectSingleNode($"//*[@name='{typeName}']//*[@name='{operationName}']//*[contains(local-name(),'documentation')]")?.InnerText;
								if (elementDocumentation!=null)
								{
									elementDocumentation = toSummary(elementDocumentation,elementMatch.Groups["space"].Value);
									classContent = Regex.Replace(classContent,@$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*public (?<returnType>\S+) {methodName}\b)",m => elementDocumentation+m.Groups["remainder"].Value,RegexOptions.Singleline|RegexOptions.Multiline);
								}
							}
						}

						// enum documentation
						foreach (Match enumMatch in Regex.Matches(classContent,@"\[(?<xmlElementAttribute>(System.Xml.Serialization.)?XmlEnum(Attribute)?\()(""(?<enumValueName>[^""]+)"")?[^\)]*\)\](?<space>\s+)(?<enumValue>\S+),"))
						{
							string enumValue = enumMatch.Groups["enumValueName"].Value;
							string enumValueName = enumMatch.Groups["enumValue"].Value;
							string enumDocumentation = xmlDocument.SelectSingleNode($"/*/*[@name='{rootName}']//*[@value='{enumValueName}']//*[contains(local-name(),'documentation')]")?.InnerText;

							if (enumDocumentation!=null)
							{
								enumDocumentation = toSummary(enumDocumentation,enumMatch.Groups["space"].Value);
								classContent = Regex.Replace(classContent,@$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*{enumValue}\b)",m => enumDocumentation+m.Groups["remainder"].Value,RegexOptions.Singleline|RegexOptions.Multiline);
							}
						}

						fileContent = fileContent.Replace(classMatch.Value,classContent);

						// complex type documentation
						string classDocumentation = xmlDocument.SelectSingleNode($"/*/*[@name='{rootName}']//*[contains(name(),'documentation')]")?.InnerText;
						if (classDocumentation!=null)
						{
							classDocumentation = toSummary(classDocumentation,classMatch.Groups["space"].Value);
							fileContent = Regex.Replace(fileContent,@$"(?<remarks>/// <remarks/>)(?<remainder>(\s*\[[^\n]+\])*\s*public (partial class|enum) {className}\b)",m => classDocumentation+m.Groups["remainder"].Value,RegexOptions.Singleline|RegexOptions.Multiline);
						}
					}
				}

				// use usings
				string[] usings = new[] { "System","System.CodeDom.Compiler","System.ComponentModel","System.Diagnostics","System.Threading","System.Web.Services","System.Web.Services.Description","System.Web.Services.Protocols","System.Xml.Schema","System.Xml.Serialization" };
				foreach (string usingNamespace in usings.OrderByDescending(u => u.Length))
					fileContent = fileContent.Replace(usingNamespace+".","");
				fileContent = Regex.Replace(fileContent,@"(using \S+;\s?\n)+",String.Join(Environment.NewLine,usings.OrderBy(u => u).Select(u => $"using {u};"))+Environment.NewLine);

				// use attribute shortcut
				fileContent = fileContent.Replace("Attribute()]","]");
				fileContent = fileContent.Replace("Attribute(","(");

				File.WriteAllText(path,fileContent,encoding);
			}
		}
	}
}

