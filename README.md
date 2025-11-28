<p align="center">
  <img src="docs/assets/checkemails.png" alt="CheckEmails" width="200"/>
</p>

# CheckEmails

A command-line tool for validating email addresses in bulk. This program allows you to check email addresses for several parameters. While this is not a 100% guarantee of a valid email, it helps to exclude clearly bad email addresses from your lists. It checks email format, detects disposable (temporary) email domains, and verifies MX records for domains.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

---

## Table of Contents

- [English](#english)
  - [Features](#features)
  - [Validation Checks](#validation-checks)
  - [Installation](#installation)
  - [Verifying downloads](#verifying-downloads)
  - [Usage](#usage)
  - [Examples](#examples)
  - [Output Files](#output-files)
  - [Data Sources](#data-sources)
- [Русский](#русский)
  - [Возможности](#возможности)
  - [Проверки валидации](#проверки-валидации)
  - [Установка](#установка)
  - [Проверка загрузок](#проверка-загрузок)
  - [Использование](#использование)
  - [Примеры](#примеры)
  - [Выходные файлы](#выходные-файлы)
  - [Источники данных](#источники-данных)

---

## English

### Features

- **Batch validation** — Process thousands of email addresses from a file
- **Single email validation** — Quickly check one email address
- **Disposable email detection** — Identify temporary/throwaway email addresses
- **MX record verification** — Check if the email domain can receive mail
- **Operations with email lists** — Merge or subtract email lists
- **Cross-platform** — Available for Windows, Linux, and macOS

### Validation Checks

The program performs three types of validation:

1. **Format validation** — Checks if the email address has a valid format (e.g., `user@domain.com`)
2. **Disposable domain detection** — Identifies emails from temporary/disposable email services (like `tempmail.com`, `guerrillamail.com`, etc.)
3. **MX record verification** — Verifies that the domain has mail exchange (MX) records configured, meaning it can actually receive emails

### Installation

Download the appropriate executable for your operating system:

| Platform | File |
|----------|------|
| Windows  | `checkemails.exe` |
| Linux    | `checkemails` |
| macOS    | `checkemails` |

No installation required — just download and run.

### Verifying downloads

Each release includes platform-specific ZIP archives along with integrity and signature files:

- `checkemails-<TAG>-<RID>.zip` — the binary archive
- `checkemails-<TAG>-<RID>.zip.sha256` — SHA256 checksum
- `checkemails-<TAG>-<RID>.zip.sig` — signature
- `checkemails-<TAG>-<RID>.zip.pem` — signing certificate

Replace `<TAG>` with the release tag (for example, `v1.0.0`) and `<RID>` with the runtime ID (`win-x64`, `linux-x64`, `osx-arm64`).

1) Verify SHA256 checksum (Linux/macOS):

```bash
sha256sum -c checkemails-v1.0.0-linux-x64.zip.sha256
# or, if your shell doesn't support -c:
sha256sum checkemails-v1.0.0-linux-x64.zip
```

macOS alternative:

```bash
shasum -a 256 checkemails-v1.0.0-osx-arm64.zip
```

Windows PowerShell:

```powershell
Get-FileHash .\checkemails-v1.0.0-win-x64.zip -Algorithm SHA256 | Format-List
# Compare the hash with the content of .sha256 file
Get-Content .\checkemails-v1.0.0-win-x64.zip.sha256
```

2) Verify signature with Cosign (any OS, requires cosign to be installed):

```bash
cosign verify-blob \
  --certificate checkemails-v1.0.0-linux-x64.zip.pem \
  --signature   checkemails-v1.0.0-linux-x64.zip.sig \
  checkemails-v1.0.0-linux-x64.zip
```

Optional strict verification:

```bash
cosign verify-blob \
  --certificate checkemails-v1.0.0-linux-x64.zip.pem \
  --signature   checkemails-v1.0.0-linux-x64.zip.sig \
  --certificate-identity "https://github.com/idev-oss/CheckEmails/.github/workflows/release-aot.yml@refs/tags/v1.0.0" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  checkemails-v1.0.0-linux-x64.zip
```

Successful verification confirms the archive was signed by this repository's GitHub Actions workflow via OIDC (keyless).

### Usage

```
checkemails [options]
```

#### General Options

| Option | Description |
|--------|-------------|
| `-h`, `--help` | Show help message and exit |
| `-v`, `--version` | Show application version and disposable list file timestamp |
| `--debug` | Enable debug mode (verbose logging with stack traces) |
| `--utc` | Use UTC time for logs and outputs (default is local time) |
| `-r`, `--refresh-disposable` | Force update of the disposable-domain cache |
| `-d`, `--disposable-domains <path>` | Use an additional custom disposable-domain list. The application also creates a `.checkemails` directory in your home directory where you can add a list of domains that you consider to be temporary or that you do not want to send emails to. |
| `-e`, `--email <address>` | Validate one email without reading files |

#### Batch Validation Options

| Option | Description |
|--------|-------------|
| `-i`, `--input <path>` | **Required.** Source file with email addresses (one per line or CSV) |
| `-o`, `--results-dir <path>` | Directory for output files. If not specified, a directory will be created in the root of the folder from where the program is launched. |

#### Options for operations with email lists

| Option | Description |
|--------|-------------|
| `--set-mode <subtract\|merge>` | Perform an operation on email lists |
| `--include <path>` | File to include; repeat for multiple inputs |
| `--exclude <path>` | File to exclude (only for subtract mode) |
| `--result <path>` | Output file for the resulting set |

### Examples

#### Validate a list of emails before adding to a mailing service

If you have a list of emails you want to check before adding them to a mailing service, you can run a batch validation. This will help you remove invalid, temporary, or inactive email addresses, improving your delivery rates.

**Windows:**
```cmd
checkemails.exe -i new_subscribers.csv -o validation_results
```

**Linux/macOS:**
```bash
./checkemails -i new_subscribers.csv -o validation_results
```
This command will process `new_subscribers.csv` and save the results in the `validation_results` directory. The `valid_emails.csv` file will contain the emails that are safe to add to your mailing list.

#### Merge multiple email lists into one, removing duplicates

Imagine you have several lists of email addresses from different sources and you want to combine them into a single master list without any duplicates.

**Windows:**
```cmd
checkemails.exe --set-mode merge --include list1.csv --include list2.csv --include list3.csv --result master_list.csv
```

**Linux/macOS:**
```bash
./checkemails --set-mode merge --include list1.csv --include list2.csv --include list3.csv --result master_list.csv
```
This will merge `list1.csv`, `list2.csv`, and `list3.csv` into a single file named `master_list.csv`, automatically removing any duplicate email addresses.

#### Exclude a list of unsubscribed users from your main email list

Suppose you have a master list of all your users' emails and a separate list of users who have unsubscribed or whose emails have bounced. You can subtract the second list from the first to get a clean list for your next campaign.

**Windows:**
```cmd
checkemails.exe --set-mode subtract --include master_list.csv --exclude unsubscribed.csv --result clean_list.csv
```

**Linux/macOS:**
```bash
./checkemails --set-mode subtract --include master_list.csv --exclude unsubscribed.csv --result clean_list.csv
```
This command subtracts the emails in `unsubscribed.csv` from `master_list.csv` and saves the result to `clean_list.csv`.

### Output Files

When running batch validation, the program creates the following files in the results directory:

| File | Description |
|------|-------------|
| `valid_emails.csv` | Email addresses that passed all validation checks |
| `invalid_emails.csv` | Email addresses with invalid format |
| `invalid_emails_disposable.csv` | Email addresses from disposable/temporary domains |
| `invalid_emails_missing_mx.csv` | Email addresses from domains without MX records |
| `info.txt` | Summary with statistics and processing details |

If `--results-dir` is not specified, output is created in `./checkemails-results-<timestamp>/`.

### Data Sources

This program uses the disposable email domains list from the community-maintained project:

**[disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains)**

The list is automatically downloaded and cached. Use `-r` or `--refresh-disposable` to force an update.

---

## Русский

### Возможности

- **Пакетная валидация** — Обработка тысяч email-адресов из файла
- **Проверка одного email** — Быстрая проверка одного адреса
- **Обнаружение одноразовых email** — Выявление временных/одноразовых почтовых адресов
- **Проверка MX-записей** — Проверка возможности домена принимать почту
- **Операции со списками email-адресов** — Объединение или вычитание списков email
- **Кроссплатформенность** — Доступно для Windows, Linux и macOS

### Проверки валидации

Программа выполняет три типа проверок:

1. **Проверка формата** — Проверяет корректность формата email-адреса (например, `user@domain.com`)
2. **Обнаружение одноразовых доменов** — Выявляет email от временных почтовых сервисов (таких как `tempmail.com`, `guerrillamail.com` и др.)
3. **Проверка MX-записей** — Проверяет наличие у домена настроенных MX-записей, то есть возможность реально принимать почту

### Установка

Скачайте исполняемый файл для вашей операционной системы:

| Платформа | Файл |
|-----------|------|
| Windows   | `checkemails.exe` |
| Linux     | `checkemails` |
| macOS     | `checkemails` |

Установка не требуется — просто скачайте и запустите.

### Проверка загрузок

Каждый релиз содержит ZIP-архивы под платформы и сопутствующие файлы проверки:

- `checkemails-<TAG>-<RID>.zip` — бинарный архив
- `checkemails-<TAG>-<RID>.zip.sha256` — контрольная сумма SHA256
- `checkemails-<TAG>-<RID>.zip.sig` — подпись
- `checkemails-<TAG>-<RID>.zip.pem` — сертификат подписи

Замените `<TAG>` на тег релиза (например, `v1.0.0`), а `<RID>` на идентификатор платформы (`win-x64`, `linux-x64`, `osx-arm64`).

1) Проверка SHA256 (Linux/macOS):

```bash
sha256sum -c checkemails-v1.0.0-linux-x64.zip.sha256
# или если оболочка не поддерживает -c:
sha256sum checkemails-v1.0.0-linux-x64.zip
```

Альтернатива для macOS:

```bash
shasum -a 256 checkemails-v1.0.0-osx-arm64.zip
```

Windows PowerShell:

```powershell
Get-FileHash .\checkemails-v1.0.0-win-x64.zip -Algorithm SHA256 | Format-List
# Сравните хеш со строкой в файле .sha256
Get-Content .\checkemails-v1.0.0-win-x64.zip.sha256
```

2) Проверка подписи Cosign (на любой ОС, требуется установленный cosign):

```bash
cosign verify-blob \
  --certificate checkemails-v1.0.0-linux-x64.zip.pem \
  --signature   checkemails-v1.0.0-linux-x64.zip.sig \
  checkemails-v1.0.0-linux-x64.zip
```

Более строгая проверка:

```bash
cosign verify-blob \
  --certificate checkemails-v1.0.0-linux-x64.zip.pem \
  --signature   checkemails-v1.0.0-linux-x64.zip.sig \
  --certificate-identity "https://github.com/idev-oss/CheckEmails/.github/workflows/release-aot.yml@refs/tags/v1.0.0" \
  --certificate-oidc-issuer "https://token.actions.githubusercontent.com" \
  checkemails-v1.0.0-linux-x64.zip
```

Успешная проверка подтверждает, что архив подписан GitHub Actions этого репозитория через OIDC (без приватных ключей).

### Использование

```
checkemails [опции]
```

#### Общие опции

| Опция | Описание |
|-------|----------|
| `-h`, `--help` | Показать справку и выйти |
| `-v`, `--version` | Показать версию приложения и дату файла списка одноразовых доменов |
| `--debug` | Включить режим отладки (подробное логирование со стеком вызовов) |
| `--utc` | Использовать UTC для логов и выходных данных (по умолчанию — локальное время) |
| `-r`, `--refresh-disposable` | Принудительно обновить кэш одноразовых доменов |
| `-d`, `--disposable-domains <путь>` | Использовать дополнительный пользовательский список одноразовых доменов. Также программа создает папку `.checkemails` в вашей домашней директории, где вы можете указать список доменов, которые вы считаете временными или на которые вы просто не хотите отправлять письма. |
| `-e`, `--email <адрес>` | Проверить один email без чтения файлов |

#### Опции пакетной валидации

| Опция | Описание |
|-------|----------|
| `-i`, `--input <путь>` | **Обязательно.** Исходный файл с email-адресами (по одному на строку или CSV) |
| `-o`, `--results-dir <путь>` | Директория для выходных файлов. Если не указать, будет создана директория в корне папки, откуда запускается программа. |

#### Опции для операций со списками email-адресов

| Опция | Описание |
|-------|----------|
| `--set-mode <subtract\|merge>` | Выполнить операцию со списками email-адресов |
| `--include <путь>` | Файл для включения; можно указать несколько раз |
| `--exclude <путь>` | Файл для исключения (только для режима subtract) |
| `--result <путь>` | Выходной файл для результирующего набора |

### Примеры

#### Проверка списка email перед добавлением в сервис рассылки

Если у вас есть список email, который вы хотите проверить перед добавлением в сервис рассылки, вы можете запустить пакетную валидацию. Это поможет удалить неверные, временные или неактивные адреса, улучшая доставляемость писем.

**Windows:**
```cmd
checkemails.exe -i new_subscribers.csv -o validation_results
```

**Linux/macOS:**
```bash
./checkemails -i new_subscribers.csv -o validation_results
```
Эта команда обработает `new_subscribers.csv` и сохранит результаты в директорию `validation_results`. Файл `valid_emails.csv` будет содержать адреса, которые можно безопасно добавлять в ваш список рассылки.

#### Объединение нескольких списков email в один с удалением дубликатов

Представьте, что у вас есть несколько списков адресов из разных источников, и вы хотите объединить их в один общий список без дубликатов.

**Windows:**
```cmd
checkemails.exe --set-mode merge --include list1.csv --include list2.csv --include list3.csv --result master_list.csv
```

**Linux/macOS:**
```bash
./checkemails --set-mode merge --include list1.csv --include list2.csv --include list3.csv --result master_list.csv
```
Эта команда объединит `list1.csv`, `list2.csv` и `list3.csv` в один файл `master_list.csv`, автоматически удалив все дублирующиеся адреса.

#### Исключение списка отписавшихся пользователей из основного списка email

Допустим, у вас есть основной список email-адресов ваших пользователей и отдельный список тех, кто отписался или чьи адреса вернулись с ошибкой. Вы можете вычесть второй список из первого, чтобы получить чистый список для следующей рассылки.

**Windows:**
```cmd
checkemails.exe --set-mode subtract --include master_list.csv --exclude unsubscribed.csv --result clean_list.csv
```

**Linux/macOS:**
```bash
./checkemails --set-mode subtract --include master_list.csv --exclude unsubscribed.csv --result clean_list.csv
```
Эта команда вычитает адреса из `unsubscribed.csv` из `master_list.csv` и сохраняет результат в `clean_list.csv`.

### Выходные файлы

При пакетной валидации программа создаёт следующие файлы в директории результатов:

| Файл | Описание |
|------|----------|
| `valid_emails.csv` | Email-адреса, прошедшие все проверки |
| `invalid_emails.csv` | Email-адреса с некорректным форматом |
| `invalid_emails_disposable.csv` | Email-адреса с одноразовых/временных доменов |
| `invalid_emails_missing_mx.csv` | Email-адреса с доменов без MX-записей |
| `info.txt` | Сводка со статистикой и деталями обработки |

Если `--results-dir` не указан, результаты создаются в `./checkemails-results-<timestamp>/`.

### Источники данных

Программа использует список одноразовых email-доменов из поддерживаемого сообществом проекта:

**[disposable-email-domains](https://github.com/disposable-email-domains/disposable-email-domains)**

Список автоматически скачивается и кэшируется. Используйте `-r` или `--refresh-disposable` для принудительного обновления.

---

## License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.
