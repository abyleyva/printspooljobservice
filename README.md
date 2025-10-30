
![Logo](https://avatars.githubusercontent.com/u/43762225?s=96&v=4) 
* * *
# AbyLeyva


# Print Services / Background Mode

This is a project to offer a background priting service. This services works like a webservices. Acces via http://localhost:port or http://localipaddres:port



## Authors

- [@abyleyva](https://www.github.com/abyleyva)


## Features

- Method GET: ***get-printers***
    - List all the printers installed on the localhost
- Method GET: ***get-local-ipaddress***
    - Get the local ip: IPV4, IPV6, can use filter *select* to set ipv4 or ipv6 or by default all
- Method POST: ***print-pdf***
    - Print a document PDF. Is necessary specify the *printerName* and *documentPDF*. You can use Method *get-printers* to see and choice a printerName
-Method POST: ***print-label***
    - Print a document EZPL or ZPL. Is necessary specify the *printerName* and *documentPDF*. You can use Method *get-printers* to see and choice a printerName.This service was Tested with Zebra's Printer

## API Reference

#### Get all printers

```http
  GET /api/printer/get-printers
```

| Parameter | Type     | Description                |
| :-------- | :------- | :------------------------- |
| `NA` | `NA` | NA |

#### Get Local IP Address

```http
  GET /api/printer/get-local-ipaddress
```

| Parameter | Type     | Description                       |
| :-------- | :------- | :-------------------------------- |
| `select`      | `string` | **Optional**. Type IP Addres options: ipv4, ipv6, by default -> all |

#### Print PDF Files 

```http
  POST /api/printer/print-pdf
```

| Parameter | Type     | Description                       |
| :-------- | :------- | :-------------------------------- |
| `documentPDF`| `string($binary)` | **Required**. Add file PDF to print|
| `printerName`| `string` | **Required**. Set Printer Name |


#### Print Label Files (EZPL or ZPL) 

```http
  POST /api/printer/print-label
```

| Parameter | Type     | Description                       |
| :-------- | :------- | :-------------------------------- |
| `documentEZPL`| `string($binary)` | **Required**. Add file ZPL to print|
| `printerName`| `string` | **Required**. Set Printer Name |



#### Print Tickets: comming soon...

```http
  POST /api/printer/print-tickets
```

| Parameter | Type     | Description                       |
| :-------- | :------- | :-------------------------------- |
| `NA`| `NA` | **NA**|





