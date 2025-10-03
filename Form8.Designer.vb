<Global.Microsoft.VisualBasic.CompilerServices.DesignerGenerated()>
Partial Class FormEditProvider
    Inherits System.Windows.Forms.Form

    Private components As System.ComponentModel.IContainer

    <System.Diagnostics.DebuggerStepThrough()>
    Private Sub InitializeComponent()
        Me.Label1 = New System.Windows.Forms.Label()
        Me.Label2 = New System.Windows.Forms.Label()
        Me.Label3 = New System.Windows.Forms.Label()
        Me.Label4 = New System.Windows.Forms.Label()
        Me.Label5 = New System.Windows.Forms.Label()
        Me.TextBoxName = New System.Windows.Forms.TextBox()
        Me.TextBoxPLMN = New System.Windows.Forms.TextBox()
        Me.TextBoxMCC = New System.Windows.Forms.TextBox()
        Me.TextBoxMNC = New System.Windows.Forms.TextBox()
        Me.TextBoxDescription = New System.Windows.Forms.TextBox()
        Me.PictureBoxLogo = New System.Windows.Forms.PictureBox()
        Me.ButtonSave = New System.Windows.Forms.Button()
        Me.ButtonCancel = New System.Windows.Forms.Button()
        CType(Me.PictureBoxLogo, System.ComponentModel.ISupportInitialize).BeginInit()
        Me.SuspendLayout()
        '
        'Label1 - Provider Name
        '
        Me.Label1.AutoSize = True
        Me.Label1.Location = New System.Drawing.Point(20, 18)
        Me.Label1.Name = "Label1"
        Me.Label1.Size = New System.Drawing.Size(102, 20)
        Me.Label1.TabIndex = 0
        Me.Label1.Text = "Provider Name"
        '
        'Label2 - PLMN
        '
        Me.Label2.AutoSize = True
        Me.Label2.Location = New System.Drawing.Point(20, 60)
        Me.Label2.Name = "Label2"
        Me.Label2.Size = New System.Drawing.Size(46, 20)
        Me.Label2.TabIndex = 1
        Me.Label2.Text = "PLMN"
        '
        'Label3 - MCC
        '
        Me.Label3.AutoSize = True
        Me.Label3.Location = New System.Drawing.Point(20, 102)
        Me.Label3.Name = "Label3"
        Me.Label3.Size = New System.Drawing.Size(39, 20)
        Me.Label3.TabIndex = 2
        Me.Label3.Text = "MCC"
        '
        'Label4 - MNC
        '
        Me.Label4.AutoSize = True
        Me.Label4.Location = New System.Drawing.Point(20, 144)
        Me.Label4.Name = "Label4"
        Me.Label4.Size = New System.Drawing.Size(40, 20)
        Me.Label4.TabIndex = 3
        Me.Label4.Text = "MNC"
        '
        'Label5 - Description
        '
        Me.Label5.AutoSize = True
        Me.Label5.Location = New System.Drawing.Point(20, 186)
        Me.Label5.Name = "Label5"
        Me.Label5.Size = New System.Drawing.Size(85, 20)
        Me.Label5.TabIndex = 4
        Me.Label5.Text = "Description"
        '
        'TextBoxName
        '
        Me.TextBoxName.Location = New System.Drawing.Point(140, 15)
        Me.TextBoxName.Name = "TextBoxName"
        Me.TextBoxName.Size = New System.Drawing.Size(300, 26)
        Me.TextBoxName.TabIndex = 5
        '
        'TextBoxPLMN
        '
        Me.TextBoxPLMN.Location = New System.Drawing.Point(140, 57)
        Me.TextBoxPLMN.Name = "TextBoxPLMN"
        Me.TextBoxPLMN.Size = New System.Drawing.Size(300, 26)
        Me.TextBoxPLMN.TabIndex = 6
        '
        'TextBoxMCC
        '
        Me.TextBoxMCC.Location = New System.Drawing.Point(140, 99)
        Me.TextBoxMCC.Name = "TextBoxMCC"
        Me.TextBoxMCC.Size = New System.Drawing.Size(140, 26)
        Me.TextBoxMCC.TabIndex = 7
        '
        'TextBoxMNC
        '
        Me.TextBoxMNC.Location = New System.Drawing.Point(300, 99)
        Me.TextBoxMNC.Name = "TextBoxMNC"
        Me.TextBoxMNC.Size = New System.Drawing.Size(140, 26)
        Me.TextBoxMNC.TabIndex = 8
        '
        'TextBoxDescription
        '
        Me.TextBoxDescription.Location = New System.Drawing.Point(140, 183)
        Me.TextBoxDescription.Multiline = True
        Me.TextBoxDescription.Name = "TextBoxDescription"
        Me.TextBoxDescription.Size = New System.Drawing.Size(300, 80)
        Me.TextBoxDescription.TabIndex = 9
        '
        'PictureBoxLogo
        '
        Me.PictureBoxLogo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle
        Me.PictureBoxLogo.Location = New System.Drawing.Point(460, 15)
        Me.PictureBoxLogo.Name = "PictureBoxLogo"
        Me.PictureBoxLogo.Size = New System.Drawing.Size(160, 120)
        Me.PictureBoxLogo.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom
        Me.PictureBoxLogo.TabIndex = 10
        Me.PictureBoxLogo.TabStop = False
        '
        'ButtonSave
        '
        Me.ButtonSave.Location = New System.Drawing.Point(460, 170)
        Me.ButtonSave.Name = "ButtonSave"
        Me.ButtonSave.Size = New System.Drawing.Size(160, 40)
        Me.ButtonSave.TabIndex = 11
        Me.ButtonSave.Text = "Save"
        Me.ButtonSave.UseVisualStyleBackColor = True
        '
        'ButtonCancel
        '
        Me.ButtonCancel.Location = New System.Drawing.Point(460, 220)
        Me.ButtonCancel.Name = "ButtonCancel"
        Me.ButtonCancel.Size = New System.Drawing.Size(160, 40)
        Me.ButtonCancel.TabIndex = 12
        Me.ButtonCancel.Text = "Cancel"
        Me.ButtonCancel.UseVisualStyleBackColor = True
        '
        'FormEditProvider
        '
        Me.ClientSize = New System.Drawing.Size(640, 280)
        Me.Controls.Add(Me.ButtonCancel)
        Me.Controls.Add(Me.ButtonSave)
        Me.Controls.Add(Me.PictureBoxLogo)
        Me.Controls.Add(Me.TextBoxDescription)
        Me.Controls.Add(Me.TextBoxMNC)
        Me.Controls.Add(Me.TextBoxMCC)
        Me.Controls.Add(Me.TextBoxPLMN)
        Me.Controls.Add(Me.TextBoxName)
        Me.Controls.Add(Me.Label5)
        Me.Controls.Add(Me.Label4)
        Me.Controls.Add(Me.Label3)
        Me.Controls.Add(Me.Label2)
        Me.Controls.Add(Me.Label1)
        Me.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog
        Me.MaximizeBox = False
        Me.MinimizeBox = False
        Me.Name = "FormEditProvider"
        Me.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent
        Me.Text = "Edit Provider"
        CType(Me.PictureBoxLogo, System.ComponentModel.ISupportInitialize).EndInit()
        Me.ResumeLayout(False)
        Me.PerformLayout()

    End Sub

    Friend WithEvents Label1 As Label
    Friend WithEvents Label2 As Label
    Friend WithEvents Label3 As Label
    Friend WithEvents Label4 As Label
    Friend WithEvents Label5 As Label
    Friend WithEvents TextBoxName As TextBox
    Friend WithEvents TextBoxPLMN As TextBox
    Friend WithEvents TextBoxMCC As TextBox
    Friend WithEvents TextBoxMNC As TextBox
    Friend WithEvents TextBoxDescription As TextBox
    Friend WithEvents PictureBoxLogo As PictureBox
    Friend WithEvents ButtonSave As Button
    Friend WithEvents ButtonCancel As Button
End Class
