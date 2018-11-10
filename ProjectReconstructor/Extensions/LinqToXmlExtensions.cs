using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ProjectReconstructor.Extensions
{
    public static class LinqToXmlExtensions
    {
        public static XDocument AddBeforElement(this XDocument xml, string elementName, XElement elementToAdd)
        {
            var root = xml.Root;
            var particularDescendant = root.Descendants(elementName).FirstOrDefault();
            particularDescendant.AddBeforeSelf(elementName);
            return xml;
        }

        public static XDocument AddItemGroupBeforeElement(this XDocument xml)
        {
            XElement itemGroup = new XElement("ItemGroup");
            var str = xml.AddBeforElement("EndInsertion", itemGroup);
            return xml;
        }

        public static XDocument AddElementsInItemGroup(this XDocument xml, List<string> items)
        {
            var itemGroup =  xml.AddItemGroupBeforeElement();

            foreach (var item in items)
            {
                var match = Regex.Match(item, @"<(\w+).*Include=("".*"")");

                var itemType = (string)match.Groups[1].Value;
                var include = match.Groups[2].Value;

                XElement newItem = new XElement(itemType, new XAttribute("Include", include));
                itemGroup.Add(newItem);
            }

            return xml;

        }

        public static XElement FirstMandatoryElement(this XElement root, string localName)
        {
            var candidates = FindMandatoryElements(root, localName);
            return candidates.First();
        }

        public static IEnumerable<XElement> MandatoryElements(this XElement root, string localName)
        {
            var candidates = FindMandatoryElements(root, localName);
            return candidates;
        }


        private static IEnumerable<XElement> FindMandatoryElements(XElement root, string localName)
        {
            var xName = XName.Get(localName, root.GetDefaultNamespace().NamespaceName);
            var candidates = root.Elements(xName).ToArray();
            //var candidates = root.Elements().Where(c => c.Name.LocalName == localName).ToArray();

            if (candidates.Length == 0)
                throw new MandatoryXElementNotFound($"Could not find the element in {root.Name.LocalName}", localName,
                    root);

            return candidates;
        }


        public static IEnumerable<XElement> FindMandatoryElementsWithAttributeName(this XElement root, string localName,
            string attributeName)
        {
            var candidates = FindMandatoryElements(root, localName);
            var xName = XName.Get(attributeName, root.GetDefaultNamespace().NamespaceName);
            return candidates.Where(c => c.Attributes(xName).Any());
        }

        public static XElement FindMandatoryElementWithAttributeName(this XElement root, string localName,
            string attributeName, string attributeValue)
        {
            var candidateElements = FindMandatoryElements(root, localName);
            var xName = XName.Get(attributeName, root.GetDefaultNamespace().NamespaceName);
            var candidate = candidateElements.Where(c => c.Attributes(xName).Any()).ToArray();
            if (candidate.Length == 0)
                throw new MandatoryXElementNotFound(
                    $"Could not find the element with and attribute named {attributeName}", attributeName, root);

            var match = candidate.Single(c => c.Attribute(attributeName).Value == attributeValue);

            return match;
        }

        /// <summary>
        /// This will be a new XElement with the given name and the same namespace
        /// you will need to add it to the root again to make it one of the elements. 
        /// </summary>
        /// <param name="root"></param>
        /// <param name="localName"></param>
        /// <returns></returns>
        private static XElement MakeElement(this XElement root, string localName)
        {
            var defaultNameSpace = root.GetDefaultNamespace();
            var name = XName.Get(localName, defaultNameSpace.NamespaceName);
            var newElement = new XElement(name);
            return newElement;
        }


        public static XDocument CopyDoc(this XDocument document)
        {
            return XDocument.Parse(document.ToString());
        }

        /// <summary>
        /// This will return the XElement with the supplied attribute name and value or
        /// a new XElement. The chaining of the XElement will be a no op so it
        /// will be up to the calledr to check if the returned value is correct. 
        /// </summary>
        /// <param name="root"></param>
        /// <param name="attributeName"></param>
        /// <param name="value"></param>
        /// <returns></returns>


        //public static FindMandatoryAttributeFromXElement(this XElement root, string attributeName)
        //{

        //}

        //public static IEnumerable<XElement> FindXElementWithMandatoryAttributes(this XElement root, string attributeName)
        //{
        //    var elements = root.Elements();
        //    var candidateElements =  elements.Where(c => c.Attributes(attributeName).Any()).ToArray();


        //}
    }
}