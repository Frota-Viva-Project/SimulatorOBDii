    // ==========================================
    // EXEMPLO DE USO CORRETO
    // ==========================================

    /*
    // OPÇÃO 1: Usar o construtor sem parâmetros (cria seu próprio BluetoothManager)
    var deviceDialog = new DeviceSelectionDialog();
    if (deviceDialog.ShowDialog() == DialogResult.OK)
    {
        var selectedDevice = deviceDialog.SelectedDevice;
        // Usar o dispositivo selecionado...
    }

    // OPÇÃO 2: Usar o construtor com parâmetro (compartilha o BluetoothManager)
    var bluetoothManager = new BluetoothManager();
    var deviceDialog = new DeviceSelectionDialog(bluetoothManager);
    if (deviceDialog.ShowDialog() == DialogResult.OK)
    {
        var selectedDevice = deviceDialog.SelectedDevice;
        // Conectar usando o mesmo manager...
        await bluetoothManager.ConnectToDeviceAsync(selectedDevice);
    }
    */

    // ==========================================
    // CLASSE BluetoothDevice NECESSÁRIA
    // ==========================================

    // Se você não tem esta classe, aqui está uma implementação básica:

    using InTheHand.Net.Bluetooth;
    using InTheHand.Net.Sockets;
    using System;
    using System.Collections.Generic;

    namespace OBDiiSimulator
    {
        
}
