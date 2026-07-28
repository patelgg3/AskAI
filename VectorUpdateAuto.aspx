<%@ Page Language="VB" AutoEventWireup="false" CodeFile="VectorUpdateAuto.aspx.vb" Inherits="VectorUpdateAuto" Async="true" %>

<!DOCTYPE html>
<html>
<head runat="server">
    <title>Bulk Update Vector Embeddings</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 30px; background-color: #f9f9f9; }
        .section { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); max-width: 600px; }
        .btn { padding: 10px 15px; background: #6c757d; color: white; border: none; border-radius: 4px; cursor: pointer; font-size: 14px; }
        .status-msg { margin-top: 15px; display: block; font-weight: bold; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        <div class="section">
            <h3>SQL Server 2025 Vector Maintenance</h3>
            <p>Click the button below to parse missing descriptions and populate your vector fields locally.</p>
            
            <!-- The button that triggers the batch execution -->
            <asp:Button ID="btnBulkUpdate" runat="server" Text="Process Missing Vectors" OnClick="btnBulkUpdate_Click" CssClass="btn" />
            
            <br />
            <!-- FIX: This exact line must exist for the VB code-behind to find it -->
            <asp:Label ID="lblStatus" runat="server" CssClass="status-msg" ForeColor="Blue"></asp:Label>
        </div>
    </form>
</body>
</html>