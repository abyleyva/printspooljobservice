# Print Spool Job Service - API Usage Examples

Complete working examples for using the Print Spool Job Service API.

---

## Table of Contents

1. [Python Examples](#python-examples)
2. [JavaScript/Node.js Examples](#javascriptnodejs-examples)
3. [cURL Examples](#curl-examples)
4. [PowerShell Examples](#powershell-examples)
5. [Advanced Scenarios](#advanced-scenarios)

---

## Python Examples

### Prerequisites

```bash
pip install requests
```

### Example 1: Get List of Printers

```python
import requests

BASE_URL = "http://localhost:5075/api/printer"

def get_printers():
	"""Get list of all available printers"""
	response = requests.get(f"{BASE_URL}/get-printers")
	response.raise_for_status()
	printers = response.json()
	print("Available Printers:")
	for printer in printers:
		print(f"  - {printer}")
	return printers

if __name__ == "__main__":
	get_printers()
```

### Example 2: Get Local IP Addresses

```python
import requests
import json

BASE_URL = "http://localhost:5075/api/printer"

def get_ip_addresses(select="all"):
	"""
	Get local IP addresses

	Args:
		select (str): 'all', 'ipv4', 'ipv6', 'primary', 'httplocal'
	"""
	params = {"select": select}
	response = requests.get(f"{BASE_URL}/get-local-ipaddress", params=params)
	response.raise_for_status()

	print(json.dumps(response.json(), indent=2))
	return response.json()

if __name__ == "__main__":
	print("=== All IPs ===")
	get_ip_addresses("all")

	print("\n=== IPv4 Only ===")
	get_ip_addresses("ipv4")
```

### Example 3: Print a PDF

```python
import requests
from pathlib import Path

BASE_URL = "http://localhost:5075/api/printer"

def print_pdf(pdf_path, printer_name):
	"""
	Print a PDF file

	Args:
		pdf_path (str): Path to PDF file
		printer_name (str): Name of printer to print to
	"""
	pdf_file = Path(pdf_path)

	if not pdf_file.exists():
		raise FileNotFoundError(f"PDF file not found: {pdf_path}")

	with open(pdf_file, 'rb') as f:
		files = {
			'documentPDF': (pdf_file.name, f, 'application/pdf'),
			'printerName': (None, printer_name)
		}
		response = requests.post(f"{BASE_URL}/print-pdf", files=files)

	response.raise_for_status()
	print(f"✓ PDF printed: {response.json()}")
	return response.json()

if __name__ == "__main__":
	printers = get_printers()
	if printers:
		print_pdf("invoice.pdf", printers[0])
```

### Example 4: Print a Label (ZPL/EZPL)

```python
import requests

BASE_URL = "http://localhost:5075/api/printer"

def print_label(label_path, printer_name):
	"""
	Print a ZPL/EZPL label

	Args:
		label_path (str): Path to label file (.txt or .zpl)
		printer_name (str): Name of printer to print to
	"""
	with open(label_path, 'rb') as f:
		files = {
			'documentEZPL': (label_path, f, 'text/plain'),
			'printerName': (None, printer_name)
		}
		response = requests.post(f"{BASE_URL}/print-label", files=files)

	response.raise_for_status()
	print(f"✓ Label printed: {response.json()}")
	return response.json()

# Example ZPL content
EXAMPLE_ZPL = """^XA
^FO50,50^A0N,25,25^FDShipping Label^FS
^BY2,3,50
^FO50,100^BC^FD123456789^FS
^FO50,200^A0N,20,20^FDWeight: 2.5 kg^FS
^XZ
"""

if __name__ == "__main__":
	# Create example label file
	with open("example_label.txt", "w") as f:
		f.write(EXAMPLE_ZPL)

	printers = get_printers()
	# Find a Zebra printer
	zebra = next((p for p in printers if 'zebra' in p.lower()), None)
	if zebra:
		print_label("example_label.txt", zebra)
```

### Example 5: Print a Thermal Receipt

```python
import requests
import json

BASE_URL = "http://localhost:5075/api/printer"

def print_receipt(printer_name, items, store_name="ACME Store"):
	"""
	Print a thermal receipt/ticket

	Args:
		printer_name (str): Name of thermal printer
		items (list): List of (code, name, qty, price) tuples
		store_name (str): Store name for header
	"""

	# Build operations list
	operations = [
		{"action": "Reset", "args": []},
		{"action": "Header", "args": [store_name]},
		{"action": "AlignCenter", "args": []},
		{"action": "ContinueLine", "args": ["="]},
		{
			"action": "HeaderItem",
			"args": [[["Item", 15, 0], ["Qty", 8, 1], ["Price", 8, 2]]]
		},
		{
			"action": "RowItem",
			"args": [list(item) for item in items]
		},
		{"action": "Body", "args": [""]},
		{"action": "AlignRight", "args": []},
		{
			"action": "Footer",
			"args": ["Thank you!", "Visit us again"]
		},
		{"action": "Feed", "args": [2]},
		{"action": "PaperCut", "args": [True]}
	]

	payload = {
		"newTicket": {
			"printerName": printer_name,
			"encoding": "UTF-8",
			"operations": operations
		}
	}

	response = requests.post(
		f"{BASE_URL}/print-ticket",
		json=payload,
		headers={"Content-Type": "application/json"}
	)

	response.raise_for_status()
	result = response.json()
	print(f"✓ Receipt printed: {result['message']}")
	return result

if __name__ == "__main__":
	items = [
		["SKU001", "Notebook", 2, 9.99],
		["SKU002", "Pen Pack", 1, 4.99],
		["SKU003", "USB Cable", 3, 7.99]
	]

	printers = get_printers()
	if printers:
		print_receipt(printers[0], items)
```

### Example 6: Upload and Use Logo

```python
import requests
from pathlib import Path

BASE_URL = "http://localhost:5075/api/printer"

def upload_logo(image_path, resource_key):
	"""
	Upload a logo to the service

	Args:
		image_path (str): Path to image file (PNG, JPEG, SVG, WebP)
		resource_key (str): Key to store the logo under
	"""
	image_file = Path(image_path)

	with open(image_file, 'rb') as f:
		files = {
			'logo': (image_file.name, f),
			'resourceKey': (None, resource_key)
		}
		response = requests.put(f"{BASE_URL}/save_logo", files=files)

	response.raise_for_status()
	print(f"✓ Logo uploaded with key: {response.json()['key']}")
	return response.json()

def get_logo_keys():
	"""Get all available logo keys"""
	response = requests.get(f"{BASE_URL}/logo-keys")
	response.raise_for_status()

	print("Available Logos:")
	for logo in response.json():
		print(f"  - {logo['key']}: {logo['filename']}")

	return response.json()

def download_logo(logo_key, output_path):
	"""Download a logo by key"""
	response = requests.get(
		f"{BASE_URL}/logo",
		params={"key": logo_key}
	)
	response.raise_for_status()

	with open(output_path, 'wb') as f:
		f.write(response.content)

	print(f"✓ Logo saved to: {output_path}")

if __name__ == "__main__":
	# Upload logo
	upload_logo("company_logo.png", "logo_company")

	# List logos
	get_logo_keys()

	# Download logo
	download_logo("logo_company", "downloaded_logo.png")
```

---

## JavaScript/Node.js Examples

### Prerequisites

```bash
npm install axios
```

### Example 1: Get Printers and Print PDF

```javascript
const axios = require('axios');
const FormData = require('form-data');
const fs = require('fs');
const path = require('path');

const BASE_URL = 'http://localhost:5075/api/printer';

async function getPrinters() {
	try {
		const response = await axios.get(`${BASE_URL}/get-printers`);
		console.log('Available Printers:', response.data);
		return response.data;
	} catch (error) {
		console.error('Error getting printers:', error.message);
		throw error;
	}
}

async function printPDF(pdfPath, printerName) {
	try {
		const form = new FormData();
		form.append('documentPDF', fs.createReadStream(pdfPath));
		form.append('printerName', printerName);

		const response = await axios.post(`${BASE_URL}/print-pdf`, form, {
			headers: form.getHeaders()
		});

		console.log('✓ PDF Printed:', response.data);
		return response.data;
	} catch (error) {
		console.error('Error printing PDF:', error.response?.data || error.message);
		throw error;
	}
}

// Usage
(async () => {
	const printers = await getPrinters();
	if (printers.length > 0) {
		await printPDF('invoice.pdf', printers[0]);
	}
})();
```

### Example 2: Print Thermal Ticket

```javascript
const axios = require('axios');

const BASE_URL = 'http://localhost:5075/api/printer';

async function printTicket(printerName, items) {
	try {
		const payload = {
			newTicket: {
				printerName,
				encoding: 'UTF-8',
				operations: [
					{ action: 'Reset', args: [] },
					{ action: 'Header', args: ['ACME Store'] },
					{ action: 'AlignCenter', args: [] },
					{ action: 'ContinueLine', args: ['='] },
					{
						action: 'HeaderItem',
						args: [[['Item', 15, 0], ['Qty', 8, 1], ['Price', 8, 2]]]
					},
					{
						action: 'RowItem',
						args: items
					},
					{ action: 'Body', args: [''] },
					{ action: 'AlignRight', args: [] },
					{ action: 'Footer', args: ['Thank you!', 'Visit again'] },
					{ action: 'Feed', args: [2] },
					{ action: 'PaperCut', args: [true] }
				]
			}
		};

		const response = await axios.post(`${BASE_URL}/print-ticket`, payload, {
			headers: { 'Content-Type': 'application/json' }
		});

		console.log('✓ Ticket Printed:', response.data);
		return response.data;
	} catch (error) {
		console.error('Error printing ticket:', error.response?.data || error.message);
		throw error;
	}
}

// Usage
(async () => {
	const items = [
		['SKU001', 'Notebook', 2, 9.99],
		['SKU002', 'Pen', 1, 4.99]
	];

	await printTicket('Epson TM-T20II', items);
})();
```

---

## cURL Examples

### Get Printers

```bash
curl -X GET http://localhost:5075/api/printer/get-printers
```

### Get Local IP Addresses

```bash
# Get all IPs
curl -X GET "http://localhost:5075/api/printer/get-local-ipaddress"

# Get IPv4 only
curl -X GET "http://localhost:5075/api/printer/get-local-ipaddress?select=ipv4"

# Get primary IP
curl -X GET "http://localhost:5075/api/printer/get-local-ipaddress?select=primary"
```

### Print PDF

```bash
curl -X POST http://localhost:5075/api/printer/print-pdf \
  -F "documentPDF=@invoice.pdf" \
  -F "printerName=Brother HL-L2350DW"
```

### Print Label (ZPL)

```bash
# Create example ZPL file
cat > label.txt << 'EOF'
^XA
^FO50,50^A0N,25,25^FDShipping Label^FS
^BY2,3,50
^FO50,100^BC^FD123456789^FS
^FO50,200^A0N,20,20^FDWeight: 2.5 kg^FS
^XZ
EOF

# Print label
curl -X POST http://localhost:5075/api/printer/print-label \
  -F "documentEZPL=@label.txt" \
  -F "printerName=Zebra GX430t"
```

### Print Thermal Receipt

```bash
curl -X POST http://localhost:5075/api/printer/print-ticket \
  -H "Content-Type: application/json" \
  -d '{
	"newTicket": {
	  "printerName": "Epson TM-T20II",
	  "encoding": "UTF-8",
	  "operations": [
		{"action": "Reset", "args": []},
		{"action": "Header", "args": ["ACME Store"]},
		{"action": "AlignCenter", "args": []},
		{"action": "ContinueLine", "args": ["="]},
		{
		  "action": "HeaderItem",
		  "args": [[["Item", 15, 0], ["Qty", 8, 1], ["Price", 8, 2]]]
		},
		{
		  "action": "RowItem",
		  "args": [
			["SKU001", "Notebook", 2, 9.99],
			["SKU002", "Pen", 1, 4.99]
		  ]
		},
		{"action": "Footer", "args": ["Thank you!"]},
		{"action": "PaperCut", "args": [true]}
	  ]
	}
  }'
```

### Upload Logo

```bash
curl -X PUT http://localhost:5075/api/printer/save_logo \
  -F "logo=@company_logo.png" \
  -F "resourceKey=logo_company"
```

### Get Logo Keys

```bash
curl -X GET http://localhost:5075/api/printer/logo-keys
```

### Download Logo

```bash
curl -X GET "http://localhost:5075/api/printer/logo?key=logo_company" \
  -o downloaded_logo.png
```

---

## PowerShell Examples

### Get Printers

```powershell
$uri = "http://localhost:5075/api/printer/get-printers"
$printers = Invoke-RestMethod -Uri $uri -Method Get
$printers | ForEach-Object { Write-Host "  - $_" }
```

### Get Local IPs

```powershell
$uri = "http://localhost:5075/api/printer/get-local-ipaddress"
$ips = Invoke-RestMethod -Uri $uri -Method Get
$ips | ConvertTo-Json | Write-Host
```

### Print PDF

```powershell
$uri = "http://localhost:5075/api/printer/print-pdf"
$pdfPath = "C:\Invoices\invoice.pdf"
$printerName = "Brother HL-L2350DW"

$form = @{
	documentPDF = Get-Item -Path $pdfPath
	printerName = $printerName
}

Invoke-RestMethod -Uri $uri -Method Post -Form $form
```

### Print Label

```powershell
$uri = "http://localhost:5075/api/printer/print-label"
$labelPath = "C:\Labels\shipping_label.txt"
$printerName = "Zebra GX430t"

$form = @{
	documentEZPL = Get-Item -Path $labelPath
	printerName = $printerName
}

Invoke-RestMethod -Uri $uri -Method Post -Form $form
```

### Print Ticket

```powershell
$uri = "http://localhost:5075/api/printer/print-ticket"
$printerName = "Epson TM-T20II"

$body = @{
	newTicket = @{
		printerName = $printerName
		encoding = "UTF-8"
		operations = @(
			@{ action = "Reset"; args = @() },
			@{ action = "Header"; args = @("ACME Store") },
			@{ action = "AlignCenter"; args = @() },
			@{ action = "ContinueLine"; args = @("=") },
			@{
				action = "HeaderItem"
				args = @(@(@("Item", 15, 0), @("Qty", 8, 1), @("Price", 8, 2)))
			},
			@{
				action = "RowItem"
				args = @(
					@("SKU001", "Notebook", 2, 9.99),
					@("SKU002", "Pen", 1, 4.99)
				)
			},
			@{ action = "Footer"; args = @("Thank you!") },
			@{ action = "PaperCut"; args = @($true) }
		)
	}
} | ConvertTo-Json -Depth 10

Invoke-RestMethod -Uri $uri -Method Post `
	-ContentType "application/json" `
	-Body $body
```

---

## Advanced Scenarios

### Scenario 1: Batch Print Multiple PDFs

```python
import requests
from pathlib import Path

def batch_print_pdfs(pdf_directory, printer_name):
	"""Print all PDFs in a directory"""
	pdf_dir = Path(pdf_directory)
	pdf_files = list(pdf_dir.glob("*.pdf"))

	for pdf_file in pdf_files:
		try:
			print(f"Printing {pdf_file.name}...")
			with open(pdf_file, 'rb') as f:
				files = {
					'documentPDF': (pdf_file.name, f, 'application/pdf'),
					'printerName': (None, printer_name)
				}
				response = requests.post(
					f"{BASE_URL}/print-pdf",
					files=files,
					timeout=30
				)
				response.raise_for_status()
				print(f"  ✓ Success")
		except Exception as e:
			print(f"  ✗ Error: {e}")

# Usage
batch_print_pdfs("./invoices", "Brother HL-L2350DW")
```

### Scenario 2: Print with Custom Logos

```python
def print_receipt_with_logo(printer_name, logo_key, items):
	"""Print receipt with embedded logo"""

	operations = [
		{"action": "Reset", "args": []},
		{"action": "PrintLogo", "args": [logo_key]},
		{"action": "AlignCenter", "args": []},
		{"action": "Header", "args": ["ACME Store"]},
		{"action": "ContinueLine", "args": ["="]},
		{
			"action": "HeaderItem",
			"args": [[["Item", 15, 0], ["Qty", 8, 1], ["Price", 8, 2]]]
		},
		{
			"action": "RowItem",
			"args": items
		},
		{"action": "Footer", "args": ["Thank you!"]},
		{"action": "PaperCut", "args": [True]}
	]

	payload = {
		"newTicket": {
			"printerName": printer_name,
			"encoding": "UTF-8",
			"operations": operations
		}
	}

	response = requests.post(f"{BASE_URL}/print-ticket", json=payload)
	response.raise_for_status()
	return response.json()

# Usage
items = [["SKU001", "Product", 1, 19.99]]
print_receipt_with_logo("Epson TM-T20II", "logo_company", items)
```

### Scenario 3: Error Handling and Retry Logic

```python
import requests
import time
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry

def create_session_with_retries():
	"""Create HTTP session with automatic retries"""
	session = requests.Session()

	retry = Retry(
		total=3,
		backoff_factor=0.5,
		status_forcelist=[500, 502, 503, 504]
	)

	adapter = HTTPAdapter(max_retries=retry)
	session.mount('http://', adapter)
	session.mount('https://', adapter)

	return session

def robust_print_pdf(pdf_path, printer_name, max_retries=3):
	"""Print PDF with robust error handling"""
	session = create_session_with_retries()

	for attempt in range(max_retries):
		try:
			with open(pdf_path, 'rb') as f:
				files = {
					'documentPDF': (Path(pdf_path).name, f, 'application/pdf'),
					'printerName': (None, printer_name)
				}
				response = session.post(
					f"{BASE_URL}/print-pdf",
					files=files,
					timeout=30
				)
				response.raise_for_status()
				print(f"✓ Successfully printed PDF")
				return response.json()

		except requests.exceptions.ConnectionError:
			print(f"Connection error (attempt {attempt + 1}/{max_retries})")
			if attempt < max_retries - 1:
				time.sleep(2 ** attempt)

		except requests.exceptions.HTTPError as e:
			if e.response.status_code == 404:
				print(f"✗ Printer '{printer_name}' not found")
				return None
			raise

		except Exception as e:
			print(f"✗ Error: {e}")
			raise

	raise Exception(f"Failed to print PDF after {max_retries} attempts")

# Usage
robust_print_pdf("invoice.pdf", "Brother HL-L2350DW")
```

---

## Response Examples

### Success Response

```json
{
  "success": true,
  "message": "Ticket processed successfully",
  "printerName": "Epson TM-T20II",
  "operationsProcessed": 12
}
```

### Error Response

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
	"printerName": [
	  "PrinterName is required"
	]
  }
}
```

---

**Last Updated**: 2024
**Service Version**: 1.0
**API Version**: v1
