using System;
using System.Drawing;
using System.Windows.Forms;

namespace SQLGuardian.Ssms.Extension;

internal enum LargeJoinDialogResult
{
    Cancel = 0,
    ExecuteAnyway = 1,
    ApplyNolock = 2
}

/// <summary>
/// Pre-execute dialog for large joins. Optional third action applies NOLOCK (advanced).
/// </summary>
internal sealed class LargeJoinWarningDialog : Form
{
    public LargeJoinDialogResult Choice { get; private set; } = LargeJoinDialogResult.Cancel;

    public LargeJoinWarningDialog(string message, bool allowNolockQuickFix)
    {
        Text = "SQLGuardian — large table join";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        Width = 520;
        Height = allowNolockQuickFix ? 280 : 240;

        var label = new Label
        {
            AutoSize = false,
            Left = 16,
            Top = 16,
            Width = 470,
            Height = 140,
            Text = message
        };

        var cancel = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Left = 16,
            Width = 110,
            Top = allowNolockQuickFix ? 180 : 170
        };
        cancel.Click += (_, __) =>
        {
            Choice = LargeJoinDialogResult.Cancel;
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var execute = new Button
        {
            Text = "Execute anyway",
            Left = allowNolockQuickFix ? 250 : 370,
            Width = 120,
            Top = cancel.Top
        };
        execute.Click += (_, __) =>
        {
            Choice = LargeJoinDialogResult.ExecuteAnyway;
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.Add(label);
        Controls.Add(cancel);
        Controls.Add(execute);

        if (allowNolockQuickFix)
        {
            var apply = new Button
            {
                Text = "Apply NOLOCK",
                Left = 136,
                Width = 110,
                Top = cancel.Top
            };
            apply.Click += (_, __) =>
            {
                Choice = LargeJoinDialogResult.ApplyNolock;
                DialogResult = DialogResult.OK;
                Close();
            };
            Controls.Add(apply);

            var note = new Label
            {
                AutoSize = false,
                Left = 16,
                Top = 210,
                Width = 470,
                Height = 30,
                ForeColor = Color.DimGray,
                Text = "Apply NOLOCK inserts WITH (NOLOCK) and cancels Execute so you can review. Dirty reads are possible."
            };
            Controls.Add(note);
        }

        AcceptButton = execute;
        CancelButton = cancel;
    }
}
