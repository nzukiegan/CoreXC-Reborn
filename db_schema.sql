-- USERS TABLE
CREATE TABLE IF NOT EXISTS users (
    user_id INT PRIMARY KEY IDENTITY(1,1),
    username NVARCHAR(100) UNIQUE NOT NULL,
    password_hash NVARCHAR(255) NOT NULL,
    full_name NVARCHAR(200) NOT NULL,
    email NVARCHAR(255) UNIQUE,
    is_active BIT NOT NULL DEFAULT 1,
    last_login DATETIME2,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- NETWORK TYPES
CREATE TABLE IF NOT EXISTS network_types (
    network_type_id INT PRIMARY KEY IDENTITY(1,1),
    network_name NVARCHAR(50) UNIQUE NOT NULL,
    description NVARCHAR(500)
);

-- OPERATORS
CREATE TABLE IF NOT EXISTS operators (
    operator_id INT PRIMARY KEY IDENTITY(1,1),
    operator_name NVARCHAR(150) NOT NULL,
    operator_code NVARCHAR(50) UNIQUE NOT NULL,
    plmn NVARCHAR(10),
    mcc INT,
    mnc INT,
    logo_url NVARCHAR(500),
    description NVARCHAR(500),
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- FREQUENCY BANDS
CREATE TABLE IF NOT EXISTS frequency_bands (
    band_id INT PRIMARY KEY IDENTITY(1,1),
    band_name NVARCHAR(100) UNIQUE NOT NULL,
    rat NVARCHAR(20) NOT NULL,
    ul_freq FLOAT NOT NULL,
    dl_freq FLOAT NOT NULL,
    ul_channel INT,
    dl_channel INT
);

-- GSM CELLS
CREATE TABLE IF NOT EXISTS gsm_cells (
    gsm_id INT PRIMARY KEY,
    ProviderName VARCHAR(100),
    plmn VARCHAR(10),
    rat VARCHAR(50),
    band_id INTEGER REFERENCES frequency_bands(band_id)
    mcc VARCHAR(3),
    mnc VARCHAR(3),
    arfcn VARCHAR(10),
    lac VARCHAR(10),
    nb_cell VARCHAR(50),
    cell_id VARCHAR(10),
    bsic VARCHAR(10),
    Timestamp DATETIME
);

-- LTE CELLS
CREATE TABLE IF NOT EXISTS lte_cells (
    lte_id INT PRIMARY KEY IDENTITY(1,1),
    provider_name NVARCHAR(100),
    plmn NVARCHAR(20),
    mcc INT,
    mnc INT,
    band_id INT,
    pci INT,
    nb_earfcn INT,
    nbsc INT,
    rat NVARCHAR(10),
    lac INT,
    cell_id INT NOT NULL,
    rsrp FLOAT,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    operator_id INT,
    created_by INT
);

-- WCDMA CELLS
CREATE TABLE IF NOT EXISTS wcdma_cells (
    wcdma_id INT PRIMARY KEY IDENTITY(1,1),
    provider_name NVARCHAR(100),
    plmn NVARCHAR(20),
    mcc INT,
    mnc INT,
    band_id INT,
    psc INT,
    earfcn INT,
    nbsc INT,
    rat NVARCHAR(10),
    lac INT,
    cell_id INT NOT NULL,
    rscp FLOAT,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    operator_id INT,
    created_by INT
);

-- BASE STATIONS
CREATE TABLE IF NOT EXISTS base_stations (
    base_station_id INT PRIMARY KEY IDENTITY(1,1),
    channel_number INT NOT NULL,
    base_station_name NVARCHAR(200) NOT NULL,
    is_gsm BIT NOT NULL DEFAULT 0,
    is_lte BIT NOT NULL DEFAULT 0,
    is_wcdma BIT NOT NULL DEFAULT 0,
    gsm_id INT NULL REFERENCES gsm_cells(gsm_id),
    lte_id INT NULL REFERENCES lte_cells(lte_id),
    wcdma_id INT NULL REFERENCES wcdma_cells(wcdma_id),
    frequency_mhz FLOAT NOT NULL,
    earfcn INT,
    mcc INT,
    bsic INT,
    mnc INT,
    cid INT,
    count INT,
    lac INT,
    name NVARCHAR(200),
    status NVARCHAR(20),
    band_id INT REFERENCES frequency_bands(band_id),
    last_updated DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- LOCATIONS
CREATE TABLE IF NOT EXISTS locations (
    location_id INT PRIMARY KEY IDENTITY(1,1),
    location_name NVARCHAR(200) UNIQUE NOT NULL,
    description NVARCHAR(500),
    latitude FLOAT,
    longitude FLOAT,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- SETTINGS
CREATE TABLE IF NOT EXISTS settings (
    setting_id INT PRIMARY KEY IDENTITY(1,1),
    setting_name NVARCHAR(100) NOT NULL UNIQUE,
    setting_value NVARCHAR(1000) NOT NULL,
    setting_group NVARCHAR(50) NOT NULL DEFAULT 'General',
    data_type NVARCHAR(20) NOT NULL DEFAULT 'string',
    is_secured BIT DEFAULT 0,
    min_value NVARCHAR(50),
    max_value NVARCHAR(50),
    options_json NVARCHAR(1000),
    description NVARCHAR(500),
    created_by NVARCHAR(50) DEFAULT 'system',
    updated_by NVARCHAR(50),
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- DEVICES
CREATE TABLE IF NOT EXISTS devices (
    device_id INT PRIMARY KEY IDENTITY(1,1),
    device_name NVARCHAR(200) NOT NULL,
    device_type_id INT,
    imei NVARCHAR(50) UNIQUE,
    serial_number NVARCHAR(100) UNIQUE,
    mac_address NVARCHAR(50),
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    updated_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- IMSI TARGETS
CREATE TABLE IF NOT EXISTS imsi_targets (
    imsi_target_id INT PRIMARY KEY IDENTITY(1,1),
    description NVARCHAR(500),
    case_ref NVARCHAR(100),
    location_id INT REFERENCES locations(location_id),
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by INT REFERENCES users(user_id)
);

CREATE TABLE IF NOT EXISTS imsi_target_numbers (
    imsi_number_id INT PRIMARY KEY IDENTITY(1,1),
    imsi_target_id INT NOT NULL REFERENCES imsi_targets(imsi_target_id),
    imsi NVARCHAR(64) NOT NULL
);

CREATE TABLE IF NOT EXISTS imsi_target_names (
    name_id INT PRIMARY KEY IDENTITY(1,1),
    imsi_target_id INT NOT NULL REFERENCES imsi_targets(imsi_target_id),
    target_name NVARCHAR(200) NOT NULL
);

-- IMEI TARGETS
CREATE TABLE IF NOT EXISTS imei_targets (
    imei_target_id INT PRIMARY KEY IDENTITY(1,1),
    description NVARCHAR(500),
    case_ref NVARCHAR(100),
    location_id INT REFERENCES locations(location_id),
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    created_by INT REFERENCES users(user_id)
);

CREATE TABLE IF NOT EXISTS imei_target_numbers (
    imei_number_id INT PRIMARY KEY IDENTITY(1,1),
    imei_target_id INT NOT NULL REFERENCES imei_targets(imei_target_id),
    imei NVARCHAR(50) NOT NULL
);

CREATE TABLE IF NOT EXISTS imei_target_names (
    name_id INT PRIMARY KEY IDENTITY(1,1),
    imei_target_id INT NOT NULL REFERENCES imei_targets(imei_target_id),
    target_name NVARCHAR(200) NOT NULL
);

-- BLACKLIST
CREATE TABLE IF NOT EXISTS blacklist (
    blacklist_id INT PRIMARY KEY IDENTITY(1,1),
    imei NVARCHAR(50),
    imsi NVARCHAR(64),
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

--WHITELIST
CREATE TABLE IF NOT EXISTS whitelist (
    whitelist_id INT PRIMARY KEY IDENTITY(1,1),
    imei NVARCHAR(50),
    imsi NVARCHAR(50),
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
)

-- TASK TYPES
CREATE TABLE IF NOT EXISTS task_types (
    task_type_id INT PRIMARY KEY IDENTITY(1,1),
    type_name NVARCHAR(20) UNIQUE NOT NULL,
    description NVARCHAR(200)
);

-- TASKS
CREATE TABLE IF NOT EXISTS tasks (
    task_id INT PRIMARY KEY IDENTITY(1,1),
    task_type_id INT NOT NULL REFERENCES task_types(task_type_id),
    network_id INT NOT NULL,
    target_name NVARCHAR(200),
    source NVARCHAR(100),
    dl INT,
    ul INT,
    ul_freq FLOAT,
    band NVARCHAR(100),
    imei NVARCHAR(50),
    target_imsi NVARCHAR(64),
    user_id INT REFERENCES users(user_id),
    case_ref NVARCHAR(100),
    location_id INT REFERENCES locations(location_id),
    action NVARCHAR(100),
    channel_id INT REFERENCES base_stations(base_station_id),
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    status NVARCHAR(30)
);

-- SCAN SESSIONS
CREATE TABLE IF NOT EXISTS scan_sessions (
    session_id INT PRIMARY KEY IDENTITY(1,1),
    session_name NVARCHAR(200) NOT NULL,
    device_id INT REFERENCES devices(device_id),
    location_id INT REFERENCES locations(location_id),
    start_time DATETIME2 NOT NULL,
    end_time DATETIME2,
    profile_id INT,
    notes NVARCHAR(MAX),
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);

-- SCAN RESULTS
CREATE TABLE IF NOT EXISTS scan_results (
    result_no BIGINT PRIMARY KEY IDENTITY(1,1),
    target_name NVARCHAR(200),
    session_id INT REFERENCES scan_sessions(session_id),
    date_event DATETIME2 NOT NULL,
    location_name NVARCHAR(255) NOT NULL,
    source NVARCHAR(100),
    provider_name NVARCHAR(100),
    mcc INT,
    mnc INT,
    imsi NVARCHAR(64),
    imei NVARCHAR(64),
    tmsi NVARCHAR(64),
    guti NVARCHAR(64),
    signal_level FLOAT,
    time_advance INT,
    event_no BIGINT,
    longitude FLOAT,
    latitude FLOAT,
    phone_model NVARCHAR(150),
    event NVARCHAR(150),
    count INT,
    db INT
);

-- Indexes for settings
CREATE INDEX idx_settings_group ON settings (setting_group);
CREATE INDEX idx_settings_group_name ON settings (setting_group, setting_name);