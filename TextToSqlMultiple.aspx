<%@ Page Language="VB" AutoEventWireup="false" CodeFile="TextToSqlMultiple.aspx.vb" Inherits="TextToSqlMultiple" Async="true" %>

<!DOCTYPE html>
<html>
<head runat="server">
    <title>Ollama Natural Language Text-to-SQL</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 30px; background-color: #f4f6f9; }
        .container { background: white; padding: 25px; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,0.1); max-width: 700px; }
        .input-box { width: 100%; padding: 10px; font-size: 14px; box-sizing: border-box; margin-bottom: 15px; }
        .btn { padding: 10px 20px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: bold; }
        .sql-output { background: #eef2f7; padding: 15px; font-family: Consolas, monospace; margin-top: 15px; border-left: 4px solid #007bff; white-space: pre-wrap; }
        .status { margin-top: 10px; font-weight: bold; color: #dc3545; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="container">
            <h3>Ask Your Catalog in Plain English</h3>
            <p>Example: <i>"Show me the top 3 items containing the word shoes ordered by name"</i></p>
            
            <asp:TextBox ID="txtUserPrompt" runat="server" TextMode="MultiLine" Rows="3" CssClass="input-box" Placeholder="Type your question here..."></asp:TextBox>
            <asp:Button ID="btnGenerateAndExecute" runat="server" Text="Generate & Run SQL" OnClick="btnGenerateAndExecute_Click" CssClass="btn" />
            
            <asp:Label ID="lblStatus" runat="server" CssClass="status"></asp:Label>
            
            <asp:Panel ID="pnlSql" runat="server" Visible="false">
                <h4>Generated T-SQL Statement:</h4>
                <div class="sql-output"><asp:Literal ID="litGeneratedSql" runat="server"></asp:Literal></div>
            </asp:Panel>

            <h4>Execution Results:</h4>
            <asp:GridView ID="gvResults" runat="server" CellPadding="6" GridLines="None" Width="100%" ForeColor="#333333">
                <HeaderStyle BackColor="#007bff" Font-Bold="True" ForeColor="White" />
                <RowStyle BackColor="#F7F9FA" />
                <AlternatingRowStyle BackColor="White" />
            </asp:GridView>

            <asp:Panel ID="pnlExport" runat="server" Visible="false" Style="margin-top:15px; margin-bottom:15px;">
    <asp:Button ID="btnExportExcel" runat="server" Text="💾 Export Results to Excel" OnClick="btnExportExcel_Click" CssClass="btn" Style="background-color: #28a745;" />
</asp:Panel>
        </div>
    </form>
</body>
</html>
