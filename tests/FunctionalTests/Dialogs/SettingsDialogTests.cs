﻿using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using NUnit.Framework;
using OfficeRibbonXEditor.Interfaces;
using OfficeRibbonXEditor.Properties;
using OfficeRibbonXEditor.ViewModels.Dialogs;

namespace OfficeRibbonXEditor.FunctionalTests.Dialogs;

public class SettingsDialogTests
{
    public static readonly string[] TestableSettings =
    {
        nameof(Settings.Default.TextColor),
        nameof(Settings.Default.BackgroundColor),
        nameof(Settings.Default.TagColor),
        nameof(Settings.Default.AttributeColor),
        nameof(Settings.Default.CommentColor),
        nameof(Settings.Default.StringColor),
        nameof(Settings.Default.EditorFontSize),
        nameof(Settings.Default.TabWidth),
        nameof(Settings.Default.AutoIndent),
        nameof(Settings.Default.PreserveAttributes),
        nameof(Settings.Default.ShowDefaultSamples),
        nameof(Settings.Default.CustomSamples),
        nameof(Settings.Default.UICulture),
    };

    private readonly Dictionary<string, object> _originalSettings = [];

    [OneTimeSetUp]
    public void SetUp()
    {
        foreach (var settingName in TestableSettings)
        {
            _originalSettings[settingName] = Settings.Default[settingName];
        }
    }

    [OneTimeTearDown]
    public void TearDown()
    {
        foreach (var settingName in TestableSettings)
        {
            Settings.Default[settingName] = _originalSettings[settingName];
        }

        Settings.Default.Save();
    }

    [TestCaseSource(nameof(TestableSettings))]
    public void CanResetSettingToCurrentValue(string settingName)
    {
        // Arrange
        var dialog = Launch();
        var current = Settings.Default[settingName];
        Settings.Default[settingName] = AlterSetting(current, settingName, dialog);
        Assume.That(Settings.Default[settingName], Is.Not.EqualTo(current));

        // Act
        dialog.ResetToCurrent();

        // Assert
        Assert.That(Settings.Default[settingName], Is.EqualTo(current));
    }

    [TestCaseSource(nameof(TestableSettings))]
    public void CanResetSettingToDefaultValue(string settingName)
    {
        // Arrange
        var dialog = Launch();
        dialog.ResetToDefault();
        var original = Settings.Default[settingName];
        var current = AlterSetting(original, settingName, dialog);
        Settings.Default[settingName] = current;
        Settings.Default.Save();
        Assume.That(Settings.Default[settingName], Is.Not.EqualTo(original));
        Assume.That(Settings.Default[settingName], Is.EqualTo(current));
        dialog.OnLoaded(new List<ITabItemViewModel>()); // This reloads current settings stored in dialog

        // Act / assert
        dialog.ResetToCurrent();
        Assert.That(Settings.Default[settingName], Is.Not.EqualTo(original));
        Assert.That(Settings.Default[settingName], Is.EqualTo(current));
        dialog.ResetToDefault();
        Assert.That(Settings.Default[settingName], Is.EqualTo(original));
        Assert.That(Settings.Default[settingName], Is.Not.EqualTo(current));
    }

    private static SettingsDialogViewModel Launch(params ITabItemViewModel[] tabs)
    {
        var dialog = new SettingsDialogViewModel();
        dialog.OnLoaded(tabs);
        return dialog;
    }

    private static object? AlterSetting(object? o, string settingName, SettingsDialogViewModel dialog)
    {
        if (settingName == nameof(Settings.Default.UICulture))
        {
            return SettingsDialogViewModel.LanguageChoices.First(x => x.Name != o as string).Name;
        }

        switch (o)
        {
            case bool b:
                return !b;
            case int i:
                return i + 1;
            case string s:
                return s + "random";
            case Color c:
                return Color.FromArgb(c.A, c.R, c.G, 255 - c.B);
            default:
                Assert.Fail($"Type {o?.GetType()} not covered by test");
                return null;
        }
    }
}