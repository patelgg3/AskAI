Imports System.Data
Imports System.Data.SqlClient
Imports System.Text
Imports System.Threading.Tasks
Imports System.Web.Script.Serialization

Public Class TextToSql
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

                                                         ' Intercept SELECT * and replace with explicit columns to hide DescriptionVector
                                                         If generatedSql.ToUpper().Contains("SELECT *") Then
                                                             generatedSql = System.Text.RegularExpressions.Regex.Replace(generatedSql, "SELECT\s+\*", "SELECT ProductID, ProductName, ProductDescription", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
                                                         ElseIf generatedSql.ToUpper().Contains("SELECT TOP") AndAlso generatedSql.Contains("*") Then
                                                             generatedSql = System.Text.RegularExpressions.Regex.Replace(generatedSql, "SELECT\s+TOP\s+(\d+)\s+\*", "SELECT TOP $1 ProductID, ProductName, ProductDescription", System.Text.RegularExpressions.RegexOptions.IgnoreCase)
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
            Dim systemInstruction As String =
    "You are a strict T-SQL database developer for SQL Server 2025. " &
    "The database table name is exactly: Products " &
    "The column fields are exactly: ProductID, ProductName, ProductDescription. " &
    "Do NOT use a column named 'Name'. Use 'ProductName'. " &
    "NEVER select the 'DescriptionVector' column. It must be hidden from results. " &
    "Respond ONLY with a plain executable SELECT query statement. No markdown blocks, no formatting."

            Using client As New System.Net.WebClient()
                client.Headers(System.Net.HttpRequestHeader.ContentType) = "application/json"
                client.Encoding = Encoding.UTF8

                Dim serializer As New JavaScriptSerializer()
                Dim payload = New With {
                    .model = OllamaModel,
                    .messages = New Object() {
                        New With {.role = "system", .content = systemInstruction},
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

                        ' Strip all markdown code indicators and formatting remnants
                        cleanSqlQuery = rawContent.Replace("```sql", "") _
                                                  .Replace("```", "") _
                                                  .Replace(vbCr, " ") _
                                                  .Replace(vbLf, " ") _
                                                  .Trim()

                        ' Enforce accurate column definitions by transforming common hallucinations
                        cleanSqlQuery = cleanSqlQuery.Replace("[Name]", "[ProductName]") _
                                                  .Replace(" Name ", " ProductName ") _
                                                  .Replace(" Name,", " ProductName,") _
                                                  .Replace(".Name", ".ProductName")
                    End If
                End If
            End Using
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine("Ollama Translation Error: " & ex.Message)
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