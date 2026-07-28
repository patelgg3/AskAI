<%@ Page Language="VB" AutoEventWireup="false" CodeFile="ProductCatalog.aspx.vb" Inherits="ProductCatalog" Async="true" %>

<!DOCTYPE html>
<html>
<head runat="server">
    <title>SQL Server 2025 Vector Search Catalog</title>
    <style>
        body { font-family: Arial, sans-serif; margin: 30px; background-color: #f9f9f9; }
        .section { background: white; padding: 20px; margin-bottom: 20px; border-radius: 8px; box-shadow: 0 2px 5px rgba(0,0,0,0.1); max-width: 600px; }
        h3 { color: #333; margin-top: 0; }
        .input-group { margin-bottom: 12px; }
        .input-group label { display: block; font-weight: bold; margin-bottom: 5px; }
        .input-group input, .input-group textarea { width: 100%; padding: 8px; box-sizing: border-box; }
        .btn { padding: 10px 15px; background: #007bff; color: white; border: none; border-radius: 4px; cursor: pointer; }
        .btn-search { background: #28a745; }
        .status-msg { margin-top: 10px; font-weight: bold; color: #d9534f; }
    </style>
</head>
<body>
    <form id="form1" runat="server">
        
        <!-- SECTION 1: ADD NEW DATA WITH AUTOMATIC LOCAL EMBEDDINGS -->
        <div class="section">
            <h3>Add New Product</h3>
            <div class="input-group">
                <label>Product Name:</label>
                <asp:TextBox ID="txtProdName" runat="server"></asp:TextBox>
            </div>
            <div class="input-group">
                <label>Description (Used for Vectorizing):</label>
                <asp:TextBox ID="txtProdDesc" runat="server" TextMode="MultiLine" Rows="3"></asp:TextBox>
            </div>
            <asp:Button ID="btnSave" runat="server" Text="Save with Vector" OnClick="btnSave_Click" CssClass="btn" />
            <br />
            <asp:Label ID="lblStatus" runat="server" CssClass="status-msg"></asp:Label>
        </div>

        <!-- SECTION 2: AI SEMANTIC SEARCH -->
        <div class="section">
            <h3>Semantic Catalog Search</h3>
            <div class="input-group">
                <label>Type what you are looking for (e.g., "outdoor workout boots"):</label>
                <asp:TextBox ID="txtSearch" runat="server"></asp:TextBox>
            </div>
            <asp:Button ID="btnSearch" runat="server" Text="AI Search Match" OnClick="btnSearch_Click" CssClass="btn btn-search" />
            <br /><br />
            
            <asp:GridView ID="gvResults" runat="server" AutoGenerateColumns="False" CellPadding="6" ForeColor="#333333" GridLines="None" Width="100%">
                <AlternatingRowStyle BackColor="White" />
                <Columns>
                    <asp:BoundField DataField="ProductName" HeaderText="Product Name" />
                    <asp:BoundField DataField="ProductDescription" HeaderText="Description" />
                    <asp:BoundField DataField="Distance" HeaderText="Vector Distance (Cosine)" DataFormatString="{0:F4}" />
                </Columns>
                <HeaderStyle BackColor="#507CD1" Font-Bold="True" ForeColor="White" />
                <RowStyle BackColor="#EFF3FB" />
            </asp:GridView>
        </div>

    </form>
</body>
</html>
