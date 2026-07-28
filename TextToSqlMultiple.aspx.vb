Imports System.Data
Imports System.Data.SqlClient
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web.Script.Serialization

Public Class TextToSqlMultiple
    Inherits System.Web.UI.Page

    Private Const SqlConnectionString As String = "Server=localhost;Database=DataExposed;Trusted_Connection=True;"
    Private Const OllamaChatEndpoint As String = "http://localhost:11434/api/chat"
    Private Const OllamaModel As String = "llama3"

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As System.EventArgs) Handles Me.Load
        lblStatus.Text = ""
    End Sub

    ' Line 20 Fix: Verified period syntax mapping for the click handler
    Protected Sub btnGenerateAndExecute_Click(sender As Object, e As EventArgs) Handles btnGenerateAndExecute.Click
        If String.IsNullOrEmpty(txtUserPrompt.Text) Then Return

        Page.RegisterAsyncTask(New PageAsyncTask(Async Function()
                                                     Try
                                                         lblStatus.ForeColor = System.Drawing.Color.Blue
                                                         lblStatus.Text = "Analyzing prompt and generating SQL structure via Ollama..."
                                                         pnlSql.Visible = False

                                                         Dim generatedSql As String = Await AskOllamaForSqlAsync(txtUserPrompt.Text)

                                                         If String.IsNullOrEmpty(generatedSql) Then
                                                             lblStatus.ForeColor = System.Drawing.Color.Red
                                                             lblStatus.Text = "Ollama failed to return a valid T-SQL query."
                                                             Return Nothing
                                                         End If

                                                         ' --- MULTI-TABLE INTERCEPTOR FIX ---
                                                         ' Intercept general * selections and explicitly define table-safe field arrays
                                                         If generatedSql.ToUpper().Contains("*") Then
                                                             Dim safeColumns As String = "p.ProductID, p.ProductName, p.Price, c.CategoryName, i.QuantityInStock, i.WarehouseLocation"

                                                             If generatedSql.ToUpper().Contains("SELECT TOP") Then
                                                                 ' Safely preserves any SELECT TOP numbers while dropping the asterisk
                                                                 generatedSql = System.Text.RegularExpressions.Regex.Replace(generatedSql, "SELECT\s+TOP\s+(\d+)\s+\*", "SELECT TOP $1 " & safeColumns, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                                                             Else
                                                                 generatedSql = System.Text.RegularExpressions.Regex.Replace(generatedSql, "SELECT\s+\*", "SELECT " & safeColumns, System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                                                             End If
                                                         End If

                                                         litGeneratedSql.Text = Server.HtmlEncode(generatedSql)
                                                         pnlSql.Visible = True
                                                         lblStatus.Text = "Executing generated query against local database..."

                                                         ' Open data adapter stream block
                                                         Using conn As New SqlConnection(SqlConnectionString)
                                                             Using cmd As New SqlCommand(generatedSql, conn)
                                                                 Using sda As New SqlDataAdapter(cmd)
                                                                     ' CRUCIAL DEClARATION LINE
                                                                     Dim dt As New DataTable()

                                                                     Await conn.OpenAsync()
                                                                     sda.Fill(dt)

                                                                     ' Bind dataset layout natively to your frontend grid view template
                                                                     gvResults.DataSource = dt
                                                                     gvResults.DataBind()

                                                                     ' SAVE STATE FIX: Must live inside this block while 'dt' is active in memory
                                                                     Session("ActiveSearchResults") = dt
                                                                     pnlExport.Visible = (dt.Rows.Count > 0)
                                                                 End Using
                                                             End Using
                                                         End Using

                                                         lblStatus.ForeColor = System.Drawing.Color.Green
                                                         lblStatus.Text = "Query processed successfully!"

                                                     Catch ex As SqlException
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "SQL Server Execution Error: " & ex.Message
                                                     Catch ex As Exception
                                                         lblStatus.ForeColor = System.Drawing.Color.Red
                                                         lblStatus.Text = "System Processing Failure: " & ex.Message
                                                     End Try

                                                     Return Nothing
                                                 End Function))
    End Sub

    Private Async Function AskOllamaForSqlAsync(userQuestion As String) As Task(Of String)
        Dim cleanSqlQuery As String = ""

        Try
            ' 1. BUILD A DETAILED SCHEMA MAP: Define columns, keys, and relational joins for the LLM
            Dim systemInstruction As New StringBuilder()
            systemInstruction.Append("You are a strict T-SQL developer for SQL Server 2025. ")
            systemInstruction.Append("The database schema has 3 tables. Rules for column selection are precise: ")

            ' Table 1 Context
            systemInstruction.Append("Table: Categories | Columns: CategoryID (INT), CategoryName (NVARCHAR). ")

            ' Table 2 Context
            systemInstruction.Append("Table: Products | Columns: ProductID (INT), ProductName (NVARCHAR), ProductDescription (NVARCHAR), CategoryID (INT), Price (DECIMAL). ")
            systemInstruction.Append("Note: Products.CategoryID joins with Categories.CategoryID. ")
            systemInstruction.Append("CRUCIAL: NEVER select the column 'DescriptionVector' from the Products table. ")

            ' Table 3 Context
            systemInstruction.Append("Table: Inventory | Columns: InventoryID (INT), ProductID (INT), WarehouseLocation (NVARCHAR), QuantityInStock (INT). ")
            systemInstruction.Append("Note: Inventory.ProductID joins with Products.ProductID. ")

            ' Formatting Rules
            systemInstruction.Append("If the user question requires combining metrics, write standard INNER JOIN statements. ")
            systemInstruction.Append("Respond ONLY with the plain executable T-SQL statement. No markdown tags like ```sql, no explanations, no comments.")

            Using client As New System.Net.WebClient()
                client.Headers(System.Net.HttpRequestHeader.ContentType) = "application/json"
                client.Encoding = Encoding.UTF8

                Dim serializer As New JavaScriptSerializer()
                Dim payload = New With {
                    .model = OllamaModel,
                    .messages = New Object() {
                        New With {.role = "system", .content = systemInstruction.ToString()},
                        New With {.role = "user", .content = userQuestion}
                    },
                    .stream = False
                }

                Dim jsonRequest As String = serializer.Serialize(payload)
                Dim jsonResponse As String = Await Task.Run(Function()
                                                                Return client.UploadString(OllamaChatEndpoint, "POST", jsonRequest)
                                                            End Function)

                Dim root As Dictionary(Of String, Object) = serializer.Deserialize(Of Dictionary(Of String, Object))(jsonResponse)
                If root IsNot Nothing AndAlso root.ContainsKey("message") Then
                    Dim messageObj As Dictionary(Of String, Object) = CType(root("message"), Dictionary(Of String, Object))

                    If messageObj IsNot Nothing AndAlso messageObj.ContainsKey("content") Then
                        Dim rawContent As String = messageObj("content").ToString().Trim()

                        ' Strip any markdown tags if the model hallucinates them anyway
                        cleanSqlQuery = rawContent.Replace("```sql", "").Replace("```", "").Replace(vbCr, " ").Replace(vbLf, " ").Trim()

                        ' Clean common column hallucinations to protect schema integrity
                        cleanSqlQuery = cleanSqlQuery.Replace("[Name]", "[ProductName]") _
                                                  .Replace(" Name ", " ProductName ") _
                                                  .Replace(".Name", ".ProductName")
                    End If
                End If
            End Using
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine("Multi-Table Prompt Error: " & ex.Message)
        End Try

        If cleanSqlQuery.EndsWith(";") Then
            cleanSqlQuery = cleanSqlQuery.Substring(0, cleanSqlQuery.Length - 1).Trim()
        End If

        Return cleanSqlQuery
    End Function

    Protected Sub btnExportExcel_Click(sender As Object, e As EventArgs) Handles btnExportExcel.Click
        Try
            ' 1. Retrieve the saved datatable state from memory
            Dim dt As DataTable = CType(Session("ActiveSearchResults"), DataTable)

            If dt IsNot Nothing AndAlso dt.Rows.Count > 0 Then
                ' 2. Clear the outbound web server buffer stream entirely
                Response.Clear()
                Response.Buffer = True

                ' Configure standard HTTP download headers for Microsoft Excel filename routing
                Response.AddHeader("content-disposition", "attachment;filename=CatalogSearch_Results.csv")
                Response.Charset = ""
                Response.ContentType = "application/text"

                Dim sb As New StringBuilder()

                ' 3. Construct the spreadsheet column header labels text array line
                For k As Integer = 0 To dt.Columns.Count - 1
                    sb.Append(dt.Columns(k).ColumnName & ",")
                Next
                ' Append a standard carriage return newline marker
                sb.Append(vbCrLf)

                ' 4. Iterate over rows to construct clean comma-delimited row columns text
                For i As Integer = 0 To dt.Rows.Count - 1
                    For k As Integer = 0 To dt.Columns.Count - 1
                        ' Extract value and sanitize text strings by scrubbing explicit inner quote marks
                        Dim cellValue As String = dt.Rows(i)(k).ToString().Replace("""", """""")

                        ' Enclose columns in quotes to protect punctuation structure integrity
                        sb.Append("""" & cellValue & """,")
                    Next
                    sb.Append(vbCrLf)
                Next

                ' 5. Write data stream block directly into browser pipeline download and exit
                Response.Output.Write(sb.ToString())
                Response.Flush()
                Response.End()
            Else
                lblStatus.ForeColor = System.Drawing.Color.Red
                lblStatus.Text = "Export Failed: No active data grid rows found in memory cache."
            End If
        Catch ex As System.Threading.ThreadAbortException
            ' Suppress standard thread termination flags thrown natively by Response.End operations
        Catch ex As Exception
            lblStatus.ForeColor = System.Drawing.Color.Red
            lblStatus.Text = "Export Error: " & ex.Message
        End Try
    End Sub
End Class