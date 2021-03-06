﻿/*
 * WordprocessingDocumentExtensions.cs - Extensions for WordprocessingDocument
 * 
 * Copyright 2014 Thomas Barnekow
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 * 
 * Developer: Thomas Barnekow
 * Email: thomas<at/>barnekow<dot/>info
 * 
 * Version: 1.0.01
 */

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomXmlDataProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.IO;
using System.IO.Packaging;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.VariantTypes;
using OpenXmlPowerTools;
using System.Drawing.Imaging;

namespace Stj.OpenXml.Extensions
{
    /// <summary>
    /// Provides extension methods for <see cref="WordprocessingDocument" /> class.
    /// </summary>
    [SuppressMessage("ReSharper", "PossiblyMistakenUseOfParamsMethod")]
    public static class WordprocessingDocumentExtensions
    {
        /// <summary>
        /// Binds content controls to a custom XML part created or updated from the given XML document.
        /// </summary>
        /// <param name="document">The WordprocessingDocument.</param>
        /// <param name="rootElement">The custom XML part's root element.</param>
        public static void BindContentControls(this WordprocessingDocument document, XElement rootElement)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (rootElement == null)
                throw new ArgumentNullException("rootElement");

            // Get or create custom XML part. This assumes that we only have a single custom
            // XML part for any given namespace.
            var destPart = document.GetCustomXmlPart(rootElement.Name.Namespace);
            if (destPart == null)
                destPart = document.CreateCustomXmlPart(rootElement);
            else
                destPart.SetRootElement(rootElement);

            // Bind the content controls to the destination part's XML document.
            document.BindContentControls(destPart);
        }

        public static XDocument ToFlatOpcDocument(this WordprocessingDocument document)
        {
            return document.ToFlatOpcDocument(new XProcessingInstruction("mso-application", "progid=\"Word.Document\""));
        }

        public static string ToFlatOpcString(this WordprocessingDocument document)
        {
            return document.ToFlatOpcDocument().ToString();
        }

        public static WordprocessingDocument FromFlatOpcString(string text)
        {
            if (text == null)
                throw new ArgumentNullException("text");

            return WordprocessingDocumentExtensions.FromFlatOpcDocument(XDocument.Parse(text), new MemoryStream(), true);
        }

        public static WordprocessingDocument FromFlatOpcString(string text, Stream stream, bool isEditable)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            if (stream == null)
                throw new ArgumentNullException("stream");

            return WordprocessingDocumentExtensions.FromFlatOpcDocument(XDocument.Parse(text), stream, isEditable);
        }

        public static WordprocessingDocument FromFlatOpcString(string text, string path, bool isEditable)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            if (path == null)
                throw new ArgumentNullException("path");

            return WordprocessingDocumentExtensions.FromFlatOpcDocument(XDocument.Parse(text), path, isEditable);
        }

        public static WordprocessingDocument FromFlatOpcString(string text, Package package)
        {
            if (text == null)
                throw new ArgumentNullException("text");
            if (package == null)
                throw new ArgumentNullException("package");

            return WordprocessingDocumentExtensions.FromFlatOpcDocument(XDocument.Parse(text), package);
        }

        public static WordprocessingDocument Clone(this WordprocessingDocument document)
        {
            return document.Clone(new MemoryStream(), true, new OpenSettings());
        }

        public static WordprocessingDocument Clone(this WordprocessingDocument document, Stream stream)
        {
            return document.Clone(stream, document.FileOpenAccess == FileAccess.ReadWrite, new OpenSettings());
        }

        public static WordprocessingDocument Clone(this WordprocessingDocument document, Stream stream, bool isEditable)
        {
            return document.Clone(stream, isEditable, new OpenSettings());
        }

        public static WordprocessingDocument Clone(this WordprocessingDocument document, Stream stream, bool isEditable, OpenSettings openSettings)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            if (openSettings == null)
                openSettings = new OpenSettings();

            document.Save();
            using (OpenXmlPackage clone = document.CreateClone(stream))
            {
                foreach (var part in document.Parts)
                    clone.AddPart(part.OpenXmlPart, part.RelationshipId);
            }
            return document.OpenClone(stream, isEditable, openSettings);
        }

        public static WordprocessingDocument CreateClone(this WordprocessingDocument document, string path)
        {
            return WordprocessingDocument.Create(path, document.DocumentType, document.AutoSave);
        }

        public static WordprocessingDocument CreateClone(this WordprocessingDocument document, Package package)
        {
            return WordprocessingDocument.Create(package, document.DocumentType, document.AutoSave);
        }

        public static WordprocessingDocument CreateClone(this WordprocessingDocument document, Stream stream)
        {
            return WordprocessingDocument.Create(stream, document.DocumentType, document.AutoSave);
        }

        public static WordprocessingDocument OpenClone(this WordprocessingDocument document, Stream stream, bool isEditable, OpenSettings openSettings)
        {
            return WordprocessingDocument.Open(stream, isEditable, openSettings);
        }

        /// <summary>
        /// Binds content controls to a custom XML part.
        /// </summary>
        /// <param name="document">The WordprocessingDocument.</param>
        /// <param name="part">The custom XML part.</param>
        public static void BindContentControls(this WordprocessingDocument document, CustomXmlPart part)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (part == null)
                throw new ArgumentNullException("part");

            var customXmlRootElement = part.GetRootElement();
            if (customXmlRootElement == null)
                throw new ArgumentException("CustomXmlPart has no root element", "part");

            var storeItemId = part.CustomXmlPropertiesPart.DataStoreItem.ItemId.Value;

            // Bind w:sdt elements contained in main document part.
            var partRootElement = document.MainDocumentPart.RootElement;
            BindContentControls(partRootElement, customXmlRootElement, storeItemId);
            partRootElement.Save();

            // Bind w:sdt elements contained in header parts.
            foreach (var headerRootElement in document.MainDocumentPart
                .HeaderParts.Select(p => p.RootElement))
            {
                BindContentControls(headerRootElement, customXmlRootElement, storeItemId);
                headerRootElement.Save();
            }

            // Bind w:sdt elements contained in footer parts.
            foreach (var footerRootElement in document.MainDocumentPart
                .FooterParts.Select(p => p.RootElement))
            {
                BindContentControls(footerRootElement, customXmlRootElement, storeItemId);
                footerRootElement.Save();
            }
        }

        /// <summary>
        /// Bind the content controls (w:sdt elements) contained in the content part's XML document to the
        /// custom XML part identified by the given storeItemId.
        /// </summary>
        /// <param name="contentRootElement">The content part's <see cref="OpenXmlPartRootElement" />.</param>
        /// <param name="customXmlRootElement">The custom XML part's root <see cref="XElement" />.</param>
        /// <param name="storeItemId">The w:storeItemId to be used for data binding.</param>
        public static void BindContentControls(OpenXmlPartRootElement contentRootElement,
            XElement customXmlRootElement, string storeItemId)
        {
            if (contentRootElement == null)
                throw new ArgumentNullException("contentRootElement");
            if (customXmlRootElement == null)
                throw new ArgumentNullException("customXmlRootElement");
            if (storeItemId == null)
                throw new ArgumentNullException("storeItemId");

            // Get all w:sdt elements with matching tags.
            var tags = customXmlRootElement.Descendants()
                .Where(e => !e.HasElements)
                .Select(e => e.Name.LocalName);
            var sdts = contentRootElement.Descendants<SdtElement>()
                .Where(sdt => sdt.SdtProperties.GetFirstChild<Tag>() != null &&
                              tags.Contains(sdt.SdtProperties.GetFirstChild<Tag>().Val.Value));

            foreach (var sdt in sdts)
            {
                // The tag value is supposed to point to a descendant element of the custom XML
                // part's root element.
                var childElementName = sdt.SdtProperties.GetFirstChild<Tag>().Val.Value;
                var leafElement = customXmlRootElement.Descendants()
                    .First(e => e.Name.LocalName == childElementName);

                // Define the list of path elements, using one of the following two options:
                // 1. The following statement is used as the basis for building the full path
                // expression (the same as built by Microsoft Word).
                var pathElements = leafElement.AncestorsAndSelf().Reverse().ToList();

                // 2. The following statement is used as the basis for building the short xPath
                // expression "//ns0:leafElement[1]".
                // List<XElement> pathElements = new List<XElement>() { leafElement };

                // Build list of namespace names for building the prefix mapping later on.
                var nsList = pathElements
                    .Where(e => e.Name.Namespace != XNamespace.None)
                    .Aggregate(new HashSet<string>(), (set, e) => set.Append(e.Name.NamespaceName))
                    .ToList();

                // Build mapping from local names to namespace indices.
                var nsDict = pathElements
                    .ToDictionary(e => e.Name.LocalName, e => nsList.IndexOf(e.Name.NamespaceName));

                // Build prefix mappings.
                var prefixMappings = nsList.Select((ns, index) => new { ns, index })
                    .Aggregate(new StringBuilder(), (sb, t) =>
                        sb.Append("xmlns:ns").Append(t.index).Append("='").Append(t.ns).Append("' "))
                    .ToString().Trim();

                // Build xPath, assuming we will always take the first element and using one
                // of the following two options (see above):
                // 1. The following statement defines the prefix for building a full path
                // expression "/ns0:path[1]/ns0:to[1]/ns0:leafElement[1]".
                Func<string, string> prefix = localName =>
                    nsDict[localName] >= 0 ? "/ns" + nsDict[localName] + ":" : "/";

                // 2. The following statement defines the prefix for building the short path
                // expression "//ns0:leafElement[1]".
                // Func<string, string> prefix = localName =>
                //     nsDict[localName] >= 0 ? "//ns" + nsDict[localName] + ":" : "//";

                var xPath = pathElements
                    .Select(e => prefix(e.Name.LocalName) + e.Name.LocalName + "[1]")
                    .Aggregate(new StringBuilder(), (sb, pc) => sb.Append(pc)).ToString();

                // Create and configure new data binding.
                var dataBinding = new DataBinding();
                if (!String.IsNullOrEmpty(prefixMappings))
                    dataBinding.PrefixMappings = prefixMappings;
                dataBinding.XPath = xPath;
                dataBinding.StoreItemId = storeItemId;

                // Add or replace data binding.
                var currentDataBinding = sdt.SdtProperties.GetFirstChild<DataBinding>();
                if (currentDataBinding != null)
                    sdt.SdtProperties.ReplaceChild(dataBinding, currentDataBinding);
                else
                    sdt.SdtProperties.Append(dataBinding);
            }
        }

        /// <summary>
        /// Creates a <see cref="CustomXmlPart" /> with the given root <see cref="XElement" />.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <param name="rootElement">The root element.</param>
        /// <returns>The newly created custom XML part.</returns>
        public static CustomXmlPart CreateCustomXmlPart(this WordprocessingDocument document, XElement rootElement)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (rootElement == null)
                throw new ArgumentNullException("rootElement");

            // Create a ds:dataStoreItem associated with the custom XML part's root element.
            var dataStoreItem = new DataStoreItem
            {
                ItemId = "{" + Guid.NewGuid().ToString().ToUpper() + "}",
                SchemaReferences = new SchemaReferences()
            };
            if (rootElement.Name.Namespace != XNamespace.None)
                dataStoreItem.SchemaReferences.Append(new SchemaReference { Uri = rootElement.Name.NamespaceName });

            // Create the custom XML part.
            var customXmlPart = document.MainDocumentPart.AddCustomXmlPart(CustomXmlPartType.CustomXml);
            customXmlPart.SetRootElement(rootElement);

            // Create the custom XML properties part.
            var propertiesPart = customXmlPart.AddNewPart<CustomXmlPropertiesPart>();
            propertiesPart.DataStoreItem = dataStoreItem;
            propertiesPart.DataStoreItem.Save();

            return customXmlPart;
        }

        /// <summary>
        /// Creates a new paragraph style with the specified style ID, primary
        /// style name, and aliases and add it to the specified style definitions
        /// part. Saves the data in the DOM tree back to the part.
        /// </summary>
        /// <param name="document">The document</param>
        /// <param name="styleId">The style's unique ID</param>
        /// <param name="styleName">The style's name</param>
        /// <param name="basedOn">The base style</param>
        /// <param name="nextStyle">The next paragraph's style</param>
        /// <returns>The newly created style</returns>
        public static Style CreateParagraphStyle(this WordprocessingDocument document,
            string styleId, string styleName, string basedOn, string nextStyle)
        {
            // Check parameters
            if (document == null)
                throw new ArgumentNullException("document");
            if (styleId == null)
                throw new ArgumentNullException("styleId");
            if (styleName == null)
                throw new ArgumentNullException("styleName");
            if (basedOn == null)
                throw new ArgumentNullException("basedOn");
            if (nextStyle == null)
                throw new ArgumentNullException("nextStyle");

            // Check whether the style already exists.
            var style = document.GetParagraphStyle(styleId);
            if (style != null)
                throw new ArgumentException("Style '" + styleId + "' already exists!", styleId);

            // Create a new paragraph style element and specify key attributes.
            style = new Style { Type = StyleValues.Paragraph, CustomStyle = true, StyleId = styleId };

            // Add key child elements
            style.Produce<StyleName>().Val = styleName;
            style.Produce<BasedOn>().Val = basedOn;
            style.Produce<NextParagraphStyle>().Val = nextStyle;

            // Add the style to the styles part
            return document.ProduceStylesElement().AppendChild(style);
        }

        /// <summary>
        /// Gets the character <see cref="Style" /> with the given id.
        /// </summary>
        /// <param name="document">The document</param>
        /// <param name="styleId">The style's id</param>
        /// <returns>The corresponding style</returns>
        public static Style GetCharacterStyle(this WordprocessingDocument document,
            string styleId)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (styleId == null)
                throw new ArgumentNullException("styleId");

            var styles = document.ProduceStylesElement();
            return styles.Elements<Style>().FirstOrDefault<Style>(
                style => style.StyleId == styleId &&
                         style.Type == StyleValues.Character);
        }

        /// <summary>
        /// Returns the <see cref="CustomXmlPart" /> having a root element with the given <see cref="XNamespace" />
        /// or null if there is no such <see cref="CustomXmlPart" />.
        /// </summary>
        /// <param name="document">The document</param>
        /// <param name="ns">The namespace</param>
        /// <returns>The corresponding part or null</returns>
        public static CustomXmlPart GetCustomXmlPart(this WordprocessingDocument document, XNamespace ns)
        {
            if (document != null && document.MainDocumentPart != null)
                return document.MainDocumentPart
                    .CustomXmlParts
                    .LastOrDefault(p => p.GetRootNamespace() == ns);

            return null;
        }

        /// <summary>
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static int GetDefaultFontSize(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var rPr = document.GetRunPropertiesBaseStyle();
            return rPr.FontSize != null ? int.Parse(rPr.FontSize.Val) : 20;
        }

        /// <summary>
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static int GetDefaultSpaceAfter(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var pPr = document.GetParagraphPropertiesBaseStyle();
            if (pPr.SpacingBetweenLines == null)
                return 0;
            if (pPr.SpacingBetweenLines.After == null)
                return 0;

            return int.Parse(pPr.SpacingBetweenLines.After);
        }

        /// <summary>
        /// </summary>
        /// <param name="document"></param>
        /// <returns></returns>
        public static int GetDefaultSpaceBefore(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var pPr = document.GetParagraphPropertiesBaseStyle();
            if (pPr.SpacingBetweenLines == null)
                return 0;
            if (pPr.SpacingBetweenLines.Before == null)
                return 0;

            return int.Parse(pPr.SpacingBetweenLines.Before);
        }

        /// <summary>
        /// Gets the last section's <see cref="PageMargin" /> element.
        /// </summary>
        /// <param name="document">The document</param>
        /// <returns>The <see cref="PageMargin" /> element</returns>
        public static PageMargin GetPageMargin(this WordprocessingDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            return document.GetSectionProperties().GetFirstChild<PageMargin>();
        }

        /// <summary>
        /// Gets the last section's <see cref="PageSize" /> element.
        /// </summary>
        /// <param name="document">The document</param>
        /// <returns>The <see cref="PageSize" /> element</returns>
        public static PageSize GetPageSize(this WordprocessingDocument document)
        {
            if (document == null) throw new ArgumentNullException("document");
            return document.GetSectionProperties().GetFirstChild<PageSize>();
        }

        /// <summary>
        /// Gets the <see cref="ParagraphPropertiesBaseStyle" /> ancestor of the document's styles element.
        /// </summary>
        /// <param name="document">The document</param>
        /// <returns>The <see cref="ParagraphPropertiesBaseStyle" /></returns>
        public static ParagraphPropertiesBaseStyle GetParagraphPropertiesBaseStyle(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var docDefaults = document.ProduceStylesElement().DocDefaults;
            return docDefaults.ParagraphPropertiesDefault.ParagraphPropertiesBaseStyle;
        }

        /// <summary>
        /// Gets the paragraph <see cref="Style" /> with the given id.
        /// </summary>
        /// <param name="document">The document</param>
        /// <param name="styleId">The style's id</param>
        /// <returns>The corresponding style</returns>
        public static Style GetParagraphStyle(this WordprocessingDocument document, string styleId)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (styleId == null)
                throw new ArgumentNullException("styleId");

            var styles = document.ProduceStylesElement();
            return styles.Elements<Style>().FirstOrDefault<Style>(
                style => style.StyleId == styleId &&
                         style.Type == StyleValues.Paragraph);
        }

        /// <summary>
        /// Gets the <see cref="RunPropertiesBaseStyle" /> ancestor of the document's styles element.
        /// </summary>
        /// <param name="document">The document</param>
        /// <returns>The <see cref="RunPropertiesBaseStyle" /></returns>
        public static RunPropertiesBaseStyle GetRunPropertiesBaseStyle(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var docDefaults = document.ProduceStylesElement().DocDefaults;
            return docDefaults.RunPropertiesDefault.RunPropertiesBaseStyle;
        }

        /// <summary>
        /// Gets the last section's properties.
        /// </summary>
        /// <param name="document">The document.</param>
        /// <returns>The last section's <see cref="SectionProperties" /> element.</returns>
        public static SectionProperties GetSectionProperties(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var body = document.MainDocumentPart.Document.Body;
            if (body == null)
                throw new ArgumentException("Invalid WordprocessingDocument: body element is missing", "document");

            return body.GetFirstChild<SectionProperties>();
        }

        /// <summary>
        /// Gets or crates the root element of the document's numbering definitions part.
        /// </summary>
        /// <param name="document">The document</param>
        /// <returns>The root element of the document's numbering definitions part</returns>
        public static Numbering ProduceNumberingElement(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var part = document.MainDocumentPart.NumberingDefinitionsPart ??
                       document.MainDocumentPart.AddNewPart<NumberingDefinitionsPart>();

            if (part.Numbering != null)
                return part.Numbering;

            part.Numbering = new Numbering();
            part.Numbering.Save();
            return part.Numbering;
        }

        public static Settings ProduceSettingsElement(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var part = document.MainDocumentPart.DocumentSettingsPart ??
                       document.MainDocumentPart.AddNewPart<DocumentSettingsPart>();

            if (part.Settings != null)
                return part.Settings;

            part.Settings = new Settings();
            part.Settings.Save();
            return part.Settings;
        }

        /// <summary>
        /// Gets or creates the root element of the document's style definitions part.
        /// </summary>
        /// <param name="document">The document</param>
        /// <returns>The root element of the document's style definitions part</returns>
        public static Styles ProduceStylesElement(this WordprocessingDocument document)
        {
            if (document == null)
                throw new ArgumentNullException("document");

            var part = document.MainDocumentPart.StyleDefinitionsPart ??
                       document.MainDocumentPart.AddNewPart<StyleDefinitionsPart>();

            if (part.Styles != null)
                return part.Styles;

            part.Styles = new Styles();
            part.Styles.Save();
            return part.Styles;
        }

        public static WordprocessingDocument FromFlatOpcDocument(XDocument document)
        {
            return WordprocessingDocumentExtensions.FromFlatOpcDocument(document, new MemoryStream(), true);
        }

        public static WordprocessingDocument FromFlatOpcDocument(XDocument document, Stream stream, bool isEditable)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (stream == null)
                throw new ArgumentNullException("stream");

            return WordprocessingDocument.Open(OpenXmlPackageExtensions.FromFlatOpcDocumentCore(document, stream), isEditable);
        }

        public static WordprocessingDocument FromFlatOpcDocument(XDocument document, string path, bool isEditable)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (path == null)
                throw new ArgumentNullException("path");

            return WordprocessingDocument.Open(OpenXmlPackageExtensions.FromFlatOpcDocumentCore(document, path), isEditable);
        }

        public static WordprocessingDocument FromFlatOpcDocument(XDocument document, Package package)
        {
            if (document == null)
                throw new ArgumentNullException("document");
            if (package == null)
                throw new ArgumentNullException("package");

            return WordprocessingDocument.Open(OpenXmlPackageExtensions.FromFlatOpcDocumentCore(document, package));
        }

        #region Tracked Revisions
        public static System.Type[] trackedRevisionsElements = new System.Type[] {
            typeof(CellDeletion),
            typeof(CellInsertion),
            typeof(CellMerge),
            typeof(CustomXmlDelRangeEnd),
            typeof(CustomXmlDelRangeStart),
            typeof(CustomXmlInsRangeEnd),
            typeof(CustomXmlInsRangeStart),
            typeof(Deleted),
            typeof(DeletedFieldCode),
            typeof(DeletedMathControl),
            typeof(DeletedRun),
            typeof(DeletedText),
            typeof(Inserted),
            typeof(InsertedMathControl),
            typeof(InsertedMathControl),
            typeof(InsertedRun),
            typeof(MoveFrom),
            typeof(MoveFromRangeEnd),
            typeof(MoveFromRangeStart),
            typeof(MoveTo),
            typeof(MoveToRangeEnd),
            typeof(MoveToRangeStart),
            typeof(MoveToRun),
            typeof(NumberingChange),
            typeof(ParagraphMarkRunPropertiesChange),
            typeof(ParagraphPropertiesChange),
            typeof(RunPropertiesChange),
            typeof(SectionPropertiesChange),
            typeof(TableCellPropertiesChange),
            typeof(TableGridChange),
            typeof(TablePropertiesChange),
            typeof(TablePropertyExceptionsChange),
            typeof(TableRowPropertiesChange),
        };

        public static bool PartHasTrackedRevisions(this OpenXmlPart part)
        {
            return part.RootElement.Descendants()
                .Any(e => trackedRevisionsElements.Contains(e.GetType()));
        }

        public static bool HasTrackedRevisions(this WordprocessingDocument document)
        {
            if (document.MainDocumentPart.PartHasTrackedRevisions())
                return true;
            foreach (var part in document.MainDocumentPart.HeaderParts)
                if (part.PartHasTrackedRevisions())
                    return true;
            foreach (var part in document.MainDocumentPart.FooterParts)
                if (part.PartHasTrackedRevisions())
                    return true;
            if (document.MainDocumentPart.EndnotesPart != null)
                if (document.MainDocumentPart.EndnotesPart.PartHasTrackedRevisions())
                    return true;
            if (document.MainDocumentPart.FootnotesPart != null)
                if (document.MainDocumentPart.FootnotesPart.PartHasTrackedRevisions())
                    return true;
            return false;
        }
        #endregion

        public static void SetCustomProperty(this WordprocessingDocument document, string name,
            object value)
        {
            var newProp = new CustomDocumentProperty() { Name = name, FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}" };
            switch (value.GetType().Name)
            {
                case "DateTime":
                    newProp.VTFileTime =
                        new VTFileTime(string.Format("{0:s}Z",
                            Convert.ToDateTime(value)));
                    break;
                case "Int32":
                    newProp.VTInt32 = new VTInt32(Convert.ToInt32(value).ToString());
                    break;
                case "Int64":
                    newProp.VTInt64 = new VTInt64(Convert.ToInt64(value).ToString());
                    break;
                case "String":
                    newProp.VTLPWSTR = new VTLPWSTR(value.ToString());
                    break;
                case "Boolean":
                    newProp.VTBool = new VTBool(Convert.ToBoolean(value).ToString().ToLower());
                    break;
                default:
                    newProp.VTLPWSTR = new VTLPWSTR(value.ToString());
                    break;
            }

            var customProps = document.CustomFilePropertiesPart;
            if (customProps == null)
            {
                customProps = document.AddCustomFilePropertiesPart();
                customProps.Properties = new Properties();
            }

            var props = customProps.Properties;
            if (props != null)
            {
                var prop =
                    props.Where(
                    p => ((CustomDocumentProperty)p).Name.Value
                        == name).FirstOrDefault();

                if (prop != null)
                    prop.Remove();
                props.AppendChild(newProp);
                int pid = 2;
                foreach (CustomDocumentProperty item in props)
                {
                    item.PropertyId = pid++;
                }
                props.Save();
            }
        }

        public static T GetCustomProperty<T>(this WordprocessingDocument document, string name)
        {

            var customProps = document.CustomFilePropertiesPart;
            var props = customProps.Properties;
            if (props != null)
            {
                var prop =
                    props.Where(
                    p => ((CustomDocumentProperty)p).Name.Value
                        == name).FirstOrDefault();

                if (prop != null)
                    return (T)Convert.ChangeType(prop.InnerText, Type.GetType(typeof(T).FullName));
            }
            return default(T);
        }

        public static void AppendHtmlImportPart(this WordprocessingDocument document, string htmlBody)
        {
            MainDocumentPart mainPart = document.MainDocumentPart;
            if (mainPart == null)
                mainPart = document.AddMainDocumentPart();
            AlternativeFormatImportPart altChunk = mainPart.AddAlternativeFormatImportPart(AlternativeFormatImportPartType.Html);
            string html = string.Concat(@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.01 Transitional//EN"" ""http://www.w3.org/TR/html4/loose.dtd"">
                <html><head><meta content=""text/html; charset=utf-8"" http-equiv=""Content-Type"" /></head><body>", htmlBody, "</body></html>");
            using (Stream stream = altChunk.GetStream())
            {
                byte[] Origem = Encoding.UTF8.GetBytes(html);
                stream.Write(Origem, 0, Origem.Length);
            }
            string rID = mainPart.GetIdOfPart(altChunk);
            if (mainPart.Document == null)
                mainPart.Document = new Document(new Body());
            mainPart.Document.Body.Append(new AltChunk() { Id = rID });
        }

        public static XElement ConvertToHtml(this WordprocessingDocument document,
            string baseUrl, DirectoryInfo imagesDirectory = null)
        {
            var doc = document.Clone();
            var mainPart = doc.MainDocumentPart;
            
            if (imagesDirectory == null)
                imagesDirectory = new DirectoryInfo(System.AppDomain.CurrentDomain.BaseDirectory);
            var imageCounter = 0;
            var settings = new WmlToHtmlConverterSettings()
            {
                ImageHandler = imageInfo =>
                {
                    ++imageCounter;
                    string extension = imageInfo.ContentType.Split('/')[1].ToLower();
                    ImageFormat imageFormat = null;
                    if (extension == "png")
                        imageFormat = ImageFormat.Png;
                    else if (extension == "gif")
                        imageFormat = ImageFormat.Gif;
                    else if (extension == "bmp")
                        imageFormat = ImageFormat.Bmp;
                    else if (extension == "jpeg")
                        imageFormat = ImageFormat.Jpeg;
                    else if (extension == "tiff")
                    {
                        // Convert tiff to gif.
                        extension = "gif";
                        imageFormat = ImageFormat.Gif;
                    }
                    else if (extension == "x-wmf")
                    {
                        extension = "wmf";
                        imageFormat = ImageFormat.Wmf;
                    }
                    else if (extension == "x-emf")
                    {
                        extension = "emf";
                        imageFormat = ImageFormat.Emf;
                    }

                    // If the image format isn't one that we expect, ignore it,
                    // and don't return markup for the link.
                    if (imageFormat == null)
                        return null;

                    string imageFileName = imagesDirectory + "/image" +
                        imageCounter.ToString() + "." + extension;
                    try
                    {
                        imageInfo.Bitmap.Save(imageFileName, imageFormat);
                    }
                    catch (System.Runtime.InteropServices.ExternalException)
                    {
                        return null;
                    }
                    string imageSource = baseUrl + "/" + imagesDirectory.Name + "/image" +
                        imageCounter.ToString() + "." + extension;

                    XElement img = new XElement(Xhtml.img,
                        new XAttribute(NoNamespace.src, imageSource),
                        imageInfo.ImgStyleAttribute,
                        imageInfo.AltText != null ?
                            new XAttribute(NoNamespace.alt, imageInfo.AltText) : null);
                    return img;
                }
            };
            return WmlToHtmlConverter.ConvertToHtml(doc, settings);
        }

        public static WordprocessingDocument CreateFromTemplate(string path)
        {
            return CreateFromTemplate(path, true);
        }

        public static WordprocessingDocument CreateFromTemplate(string path, bool isTemplateAttached)
        {
            if (path == null)
                throw new ArgumentNullException("path");

            // Check extensions as the template must have a valid Word Open XML extension.
            string extension = Path.GetExtension(path);
            if (extension != ".docx" && extension != ".docm" && extension != ".dotx" && extension != ".dotm")
                throw new ArgumentException("Illegal template file: " + path, "path");

            using (WordprocessingDocument template = WordprocessingDocument.Open(path, false))
            {
                // We've opened the template in read-only mode to let multiple processes
                // open it without running into problems.
                WordprocessingDocument document = (WordprocessingDocument)template.Clone();

                // If the template is a document rather than a template, we are done.
                if (extension == ".docx" || extension == ".docm")
                    return document;

                // Otherwise, we'll have to do some more work.
                // Firstly, we'll change the document type from Template to Document.
                document.ChangeDocumentType(WordprocessingDocumentType.Document);

                // Secondly, we'll attach the template to our new document if so desired.
                if (isTemplateAttached)
                {
                    // Create a relative or absolute external relationship to the template.
                    // TODO: Check whether relative URIs are universally supported. They work in Office 2010.
                    MainDocumentPart mainDocumentPart = document.MainDocumentPart;
                    DocumentSettingsPart documentSettingsPart = mainDocumentPart.DocumentSettingsPart;
                    ExternalRelationship relationship = documentSettingsPart.AddExternalRelationship(
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/attachedTemplate",
                        new Uri(path, UriKind.RelativeOrAbsolute));
                    documentSettingsPart.Settings.Append(
                        new DocumentFormat.OpenXml.Wordprocessing.AttachedTemplate() { Id = relationship.Id });
                }

                // We are done, so save and return.
                // TODO: Check whether it would be safe to return without saving.
                document.Save();
                return document;
            }
        }

        public static void AddMacro(this WordprocessingDocument document,
            string vbaProject, string vbaData)
        {
            var mainPart = document.MainDocumentPart;
            var vbaProjectPart = mainPart.AddNewPart<VbaProjectPart>();
            var vbaDataPart = vbaProjectPart.AddNewPart<VbaDataPart>();
            vbaProjectPart.FeedData(new MemoryStream(System.Convert.FromBase64String(vbaProject)));
            vbaDataPart.FeedData(new MemoryStream(System.Convert.FromBase64String(vbaData)));
        }

        public static void AddRibbon(this WordprocessingDocument document,
            string ribbonExtensibility)
        {
            var ribbonExtensibilityPart = document.AddRibbonExtensibilityPart();
            ribbonExtensibilityPart.FeedData(new MemoryStream(System.Convert.FromBase64String(ribbonExtensibility)));
        }
    }
}