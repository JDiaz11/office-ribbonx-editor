﻿using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO.Packaging;
using CommunityToolkit.Diagnostics;

namespace OfficeRibbonXEditor.Common;

public enum XmlPart
{
    Qat12,
    RibbonX12,
    RibbonX14,
    LastEntry // Always Last
}

public enum OfficeApplication
{
    Word,
    Excel,
    PowerPoint,
    Visio,
    Xml,
}

[SuppressMessage("SonarLint", "S5332", Justification = "The warning is about the XML schemas being insecure due to HTTP, but there's nothing to do about that")]
public class OfficeDocument : IDisposable
{
    public const string CustomUiPartRelType = "http://schemas.microsoft.com/office/2006/relationships/ui/extensibility";

    public const string CustomUi14PartRelType = "http://schemas.microsoft.com/office/2007/relationships/ui/extensibility";

    public const string QatPartRelType = "http://schemas.microsoft.com/office/2006/relationships/ui/customization";

    public const string ImagePartRelType = "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image";

    private readonly string _tempFileName;
        
    private bool _disposed;

#pragma warning disable CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable. Triggered due to UnderlyingPackage, but this is assigned in Init()
    public OfficeDocument(string fileName)
    {
        Guard.IsNotNullOrEmpty(fileName);
            
        Name = fileName;

        do
        {
            _tempFileName = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        } while (File.Exists(_tempFileName));

        File.Copy(Name, _tempFileName, true /*overwrite*/);
        File.SetAttributes(_tempFileName, FileAttributes.Normal);

        Init();
        IsDirty = false;
    }
#pragma warning restore CS8618 // Non-nullable field is uninitialized. Consider declaring as nullable.

    public Package UnderlyingPackage { get; private set; }

    public List<OfficePart>? Parts { get; private set; }

    public string Name { get; set; }

    public bool IsDirty { get; set; }

    public bool HasCustomUi
    {
        get
        {
            if (Parts == null || Parts.Count == 0)
            {
                return false;
            }

            foreach (var part in Parts)
            {
                if (part.PartType == XmlPart.RibbonX12 || part.PartType == XmlPart.RibbonX14)
                {
                    return true;
                }
            }

            return false;
        }
    }
        
    public OfficeApplication FileType => MapFileType(Path.GetExtension(Name));

    public static OfficeApplication MapFileType(string? extension)
    {
        if (extension == null)
        {
            return OfficeApplication.Xml;
        }

        if (extension.StartsWith(".do", StringComparison.OrdinalIgnoreCase))
        {
            return OfficeApplication.Word;
        }

        if (extension.StartsWith(".xl", StringComparison.OrdinalIgnoreCase))
        {
            return OfficeApplication.Excel;
        }

        if (extension.StartsWith(".pp", StringComparison.OrdinalIgnoreCase))
        {
            return OfficeApplication.PowerPoint;
        }

        if (extension.StartsWith(".vs", StringComparison.OrdinalIgnoreCase))
        {
            return OfficeApplication.Visio;
        }

        Debug.Assert(false, "Unrecognized extension passed");
        return OfficeApplication.Xml;
    }

    public void Save(string? customFileName = null, bool preserveAttributes = true)
    {
        if (string.IsNullOrEmpty(customFileName))
        {
            customFileName = Name;
        }

        if (!IsDirty && customFileName == Name)
        {
            return;
        }
            
        UnderlyingPackage.Flush();
        UnderlyingPackage.Close();

        var info = new FileInfo(customFileName);
        if (info.Exists)
        {
            File.SetAttributes(customFileName, FileAttributes.Normal);
        }

        try
        {
            File.Copy(_tempFileName, customFileName, true /*overwrite*/);
            if (info.Exists && preserveAttributes)
            {
                File.SetAttributes(customFileName, info.Attributes);
            }
        }
        finally
        {
            Init();
        }

        IsDirty = false;
    }

    public void RemoveCustomPart(XmlPart partType)
    {
        if (Parts == null)
        {
            return;
        }

        for (var i = Parts.Count - 1; i >= 0; i--)
        {
            if (Parts[i].PartType != partType)
            {
                continue;
            }

            var part = Parts[i];
            part.Remove();
                
            Parts.RemoveAt(i);

            UnderlyingPackage.Flush();
            IsDirty = true;
        }
    }

    public void SaveCustomPart(XmlPart partType, string text, bool isCreatingNewPart = false)
    {
        var targetPart = RetrieveCustomPart(partType);

        if (targetPart == null)
        {
            if (isCreatingNewPart)
            {
                targetPart = CreateCustomPart(partType);
            }
            else
            {
                return;
            }
        }

        Debug.Assert(targetPart != null, "targetPart is null when saving custom part");
        targetPart?.Save(text);
        IsDirty = true;
    }

    public OfficePart CreateCustomPart(XmlPart partType)
    {
        string relativePath;
        string relType;

        switch (partType)
        {
            case XmlPart.RibbonX12:
                relativePath = "/customUI/customUI.xml";
                relType = CustomUiPartRelType;
                break;
            case XmlPart.RibbonX14:
                relativePath = "/customUI/customUI14.xml";
                relType = CustomUi14PartRelType;
                break;
            case XmlPart.Qat12:
                relativePath = "/customUI/qat.xml";
                relType = QatPartRelType;
                break;
            default:
                throw new ArgumentException($"Unexpected {nameof(partType)}: {partType}");
        }

        var customUiUri = new Uri(relativePath, UriKind.Relative);
        var relationship = UnderlyingPackage.CreateRelationship(customUiUri, TargetMode.Internal, relType);

        OfficePart part;
        if (!UnderlyingPackage.PartExists(customUiUri))
        {
            part = new OfficePart(UnderlyingPackage.CreatePart(customUiUri, "application/xml"), partType, relationship.Id);
        }
        else
        {
            part = new OfficePart(UnderlyingPackage.GetPart(customUiUri), partType, relationship.Id);
        }

        Parts ??= [];

        Parts.Add(part);
        IsDirty = true;

        return part;
    }

    public OfficePart? RetrieveCustomPart(XmlPart partType)
    {
        Debug.Assert(Parts != null, "Document has no xmlParts to retrieve");
        if (Parts == null || Parts.Count == 0)
        {
            return null;
        }

        foreach (var part in Parts)
        {
            if (part.PartType == partType)
            {
                return part;
            }
        }

        return null;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            Name = string.Empty;
            Parts?.Clear();

            UnderlyingPackage.Close();

            try
            {
                File.Delete(_tempFileName);
            }
            catch (IOException ex)
            {
                Debug.Fail(ex.Message);
            }
        }

        _disposed = true;
    }

    private void Init()
    {
        UnderlyingPackage = Package.Open(_tempFileName, FileMode.Open, FileAccess.ReadWrite);

        if (UnderlyingPackage == null)
        {
            throw new InvalidOperationException("Failed to get package");
        }

        Parts = new List<OfficePart>();

        var collection = new[]
        {
            (CustomUi14PartRelType, XmlPart.RibbonX14),
            (CustomUiPartRelType, XmlPart.RibbonX12),
            (QatPartRelType, XmlPart.Qat12),
        };
        foreach (var (relType, partType) in collection)
        {
            var relationship = UnderlyingPackage.GetRelationshipsByType(relType).FirstOrDefault();
            if (relationship == null)
            {
                continue;
            }

            var uri = PackUriHelper.ResolvePartUri(relationship.SourceUri, relationship.TargetUri);
            if (UnderlyingPackage.PartExists(uri))
            {
                Parts.Add(new OfficePart(UnderlyingPackage.GetPart(uri), partType, relationship.Id));
            }
        }
    }
}